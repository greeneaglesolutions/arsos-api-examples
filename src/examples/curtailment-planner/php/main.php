<?php

class ApiClient {
    private $baseUrl;
    private $token;
    
    public function __construct($baseUrl) {
        $this->baseUrl = $baseUrl;
        $this->token = null;
    }
    
    public function setAuthorization($token) {
        $this->token = $token;
    }
    
    public function get($endpoint, $headers = []) {
        $url = $this->baseUrl . $endpoint;
        
        $ch = curl_init();
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_HTTPHEADER, $this->buildHeaders($headers));
        
        $response = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);
        
        return [
            'status_code' => $httpCode,
            'body' => $response
        ];
    }
    
    public function post($endpoint, $data = null, $headers = []) {
        $url = $this->baseUrl . $endpoint;
        
        $ch = curl_init();
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_POST, true);
        curl_setopt($ch, CURLOPT_HTTPHEADER, $this->buildHeaders($headers));
        
        if ($data !== null) {
            curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($data));
        }
        
        $response = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);
        
        return [
            'status_code' => $httpCode,
            'body' => $response
        ];
    }
    
    public function postFile($endpoint, $filePath, $headers = []) {
        $url = $this->baseUrl . $endpoint;
        
        if (!file_exists($filePath)) {
            throw new Exception("File not found: " . $filePath);
        }
        
        $ch = curl_init();
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_POST, true);
        curl_setopt($ch, CURLOPT_HTTPHEADER, $this->buildHeaders($headers));
        
        // Create file upload
        $postData = [
            'file' => new CURLFile($filePath)
        ];
        curl_setopt($ch, CURLOPT_POSTFIELDS, $postData);
        
        $response = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);
        
        return [
            'status_code' => $httpCode,
            'body' => $response
        ];
    }
    
    private function buildHeaders($additionalHeaders = []) {
        $headers = array_merge([
            'Content-Type: application/json'
        ], $additionalHeaders);
        
        if ($this->token) {
            $headers[] = 'x-authorization: Bearer ' . $this->token;
        }
        
        return $headers;
    }
}

class ConfigLoader {
    private $configPath;
    
    public function __construct($configPath = "config.json") {
        $this->configPath = $configPath;
    }
    
    public function loadConfig() {
        if (!file_exists($this->configPath)) {
            throw new Exception("Config file not found: " . $this->configPath);
        }
        
        $configContent = file_get_contents($this->configPath);
        $config = json_decode($configContent, true);
        
        if (json_last_error() !== JSON_ERROR_NONE) {
            throw new Exception("Invalid JSON in config file: " . $this->configPath);
        }
        
        return $config;
    }
}

function main() {
    global $argv;
    
    // Get config path and CSV file path from command line arguments
    $configPath = isset($argv[1]) ? $argv[1] : "config.json";
    $csvFilePath = isset($argv[2]) ? $argv[2] : "curtailment-template.csv";
    
    try {
        // Load configuration
        $configLoader = new ConfigLoader($configPath);
        $config = $configLoader->loadConfig();
        
        // Log the config content
        echo "Configuration loaded:\n";
        echo json_encode($config, JSON_PRETTY_PRINT) . "\n";
        
        // Initialize API client
        $apiClient = new ApiClient($config['api']['baseUrl']);
        
        // Login to get authorization token
        $loginData = [
            "user" => $config['auth']['user'],
            "pass" => $config['auth']['password']
        ];
        
        $response = $apiClient->post("/Auth", $loginData);
        
        if ($response['status_code'] == 200) {
            $loginResponse = json_decode($response['body'], true);
            $token = $loginResponse['token'] ?? null;
            
            if ($token) {
                $apiClient->setAuthorization($token);
                echo "Successfully logged in and set authorization token\n";
                
                // Import curtailments from CSV
                echo "\nImporting curtailments from: " . $csvFilePath . "\n";
                $importResponse = $apiClient->postFile("/curtailment/csv", $csvFilePath);
                
                if ($importResponse['status_code'] == 200) {
                    $importResult = json_decode($importResponse['body'], true);
                    echo "Curtailment import completed:\n";
                    echo "Errors: " . count($importResult['errors'] ?? []) . "\n";
                    echo "Schedules imported: " . count($importResult['schedules'] ?? []) . "\n";
                    
                    if (!empty($importResult['errors'])) {
                        echo "\nImport errors:\n";
                        foreach ($importResult['errors'] as $error) {
                            echo "  - " . $error . "\n";
                        }
                    }
                } else {
                    echo "Curtailment import failed with status code: " . $importResponse['status_code'] . "\n";
                    echo "Response: " . $importResponse['body'] . "\n";
                }
                
                // Get site information first
                echo "\nFetching site information...\n";
                $sitesResponse = $apiClient->get("/asset/site");
                
                if ($sitesResponse['status_code'] == 200) {
                    $sitesData = json_decode($sitesResponse['body'], true);
                    echo "Found " . count($sitesData) . " sites:\n";
                    foreach ($sitesData as $site) {
                        echo "  - " . $site['name'] . " (ID: " . $site['id'] . ")\n";
                    }
                    
                    // Extract site IDs for curtailment retrieval
                    $siteIds = array_column($sitesData, 'id');
                    
                    // Retrieve curtailment schedules using site IDs
                    echo "\nRetrieving curtailment schedules...\n";
                    $schedulePayload = [
                        "pageSize" => 50,
                        "expanded" => [],
                        "sites" => $siteIds
                    ];
                    
                    $scheduleResponse = $apiClient->post("/curtailment/by-site", $schedulePayload);
                    
                    if ($scheduleResponse['status_code'] == 200) {
                        $scheduleResult = json_decode($scheduleResponse['body'], true);
                        echo "Curtailment schedules retrieved:\n";
                        echo json_encode($scheduleResult, JSON_PRETTY_PRINT) . "\n";
                    } else {
                        echo "Failed to retrieve schedules with status code: " . $scheduleResponse['status_code'] . "\n";
                        echo "Response: " . $scheduleResponse['body'] . "\n";
                    }
                } else {
                    echo "Failed to fetch sites with status code: " . $sitesResponse['status_code'] . "\n";
                    echo "Response: " . $sitesResponse['body'] . "\n";
                }
                
            } else {
                echo "Login successful but no token received\n";
            }
        } else {
            echo "Login failed with status code: " . $response['status_code'] . "\n";
            echo "Response: " . $response['body'] . "\n";
        }
        
    } catch (Exception $e) {
        echo "Error: " . $e->getMessage() . "\n";
    }
}

// Run the main function
main(); 