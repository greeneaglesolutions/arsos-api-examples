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
        curl_setopt($ch, CURLOPT_HTTPHEADER, $this->buildHeaders($headers, false));

        $postData = [
            'file' => new CURLFile($filePath, 'application/xml', basename($filePath))
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

    private function buildHeaders($additionalHeaders = [], $includeContentType = true) {
        $headers = [];
        if ($includeContentType) {
            $headers[] = 'Content-Type: application/json';
        }
        $headers = array_merge($headers, $additionalHeaders);

        if ($this->token) {
            $headers[] = 'Authorization: Bearer ' . $this->token;
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

    $configPath = isset($argv[1]) ? $argv[1] : "config.json";
    $xmlFilePath = isset($argv[2]) ? $argv[2] : "agc-status-request-sample.xml";

    try {
        $configLoader = new ConfigLoader($configPath);
        $config = $configLoader->loadConfig();

        echo "Configuration loaded:\n";
        echo json_encode($config, JSON_PRETTY_PRINT) . "\n";

        $apiClient = new ApiClient($config['api']['baseUrl']);

        // Authenticate
        $loginData = [
            "user" => $config['auth']['user'],
            "pass" => $config['auth']['password']
        ];

        $response = $apiClient->post("/Auth", $loginData);

        if ($response['status_code'] != 200) {
            echo "Login failed with status code: " . $response['status_code'] . "\n";
            echo "Response: " . $response['body'] . "\n";
            return;
        }

        $loginResponse = json_decode($response['body'], true);
        $token = $loginResponse['token'] ?? null;

        if (!$token) {
            echo "Login successful but no token received\n";
            return;
        }

        $apiClient->setAuthorization($token);
        echo "Successfully logged in and set authorization token\n";

        // Upload XML file
        echo "\nUploading AGC status request from: " . $xmlFilePath . "\n";
        $uploadResponse = $apiClient->postFile("/api/agc-status-request/upload", $xmlFilePath);

        if ($uploadResponse['status_code'] == 200) {
            $uploadResult = json_decode($uploadResponse['body'], true);
            echo "Upload completed:\n";
            echo "  Processed: " . ($uploadResult['processedCount'] ?? 0) . " UPROGs\n";
            echo "  Skipped:   " . ($uploadResult['skippedCount'] ?? 0) . " UPROGs\n";
        } else {
            echo "Upload failed with status code: " . $uploadResponse['status_code'] . "\n";
            echo "Response: " . $uploadResponse['body'] . "\n";
            return;
        }

        // Query all UPROGs for a given gas day and quarter-hourly position
        $date = "2026-04-27";
        $quarterHourly = 1;
        echo "\nQuerying all UPROGs for {$date}, quarter-hourly position {$quarterHourly}...\n";
        $allResponse = $apiClient->get("/api/agc-status-request/{$date}/{$quarterHourly}");

        if ($allResponse['status_code'] == 200) {
            $values = json_decode($allResponse['body'], true);
            echo "Found " . count($values) . " UPROG(s):\n";
            foreach ($values as $entry) {
                $value = $entry['value'] ?? null;
                $status = $value === 1 ? "On" : ($value === -1 ? "Off" : "No data");
                echo "  - " . $entry['sitePath'] . ": " . $status . "\n";
            }
        } else {
            echo "Query failed with status code: " . $allResponse['status_code'] . "\n";
            echo "Response: " . $allResponse['body'] . "\n";
        }

        // Query a specific UPROG
        $sitePath = "UPROG Danubio";
        echo "\nQuerying specific UPROG '{$sitePath}' for {$date}, position {$quarterHourly}...\n";
        $siteResponse = $apiClient->get(
            "/api/agc-status-request/" . urlencode($sitePath) . "/{$date}/{$quarterHourly}"
        );

        if ($siteResponse['status_code'] == 200) {
            $value = json_decode($siteResponse['body'], true);
            $status = $value === 1 ? "On" : ($value === -1 ? "Off" : "No data");
            echo "  Status: " . $status . "\n";
        } elseif ($siteResponse['status_code'] == 404) {
            echo "  UPROG not found: " . $siteResponse['body'] . "\n";
        } else {
            echo "Query failed with status code: " . $siteResponse['status_code'] . "\n";
            echo "Response: " . $siteResponse['body'] . "\n";
        }

    } catch (Exception $e) {
        echo "Error: " . $e->getMessage() . "\n";
    }
}

// Run the main function
main();
