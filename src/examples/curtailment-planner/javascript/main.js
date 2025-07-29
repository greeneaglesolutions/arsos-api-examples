const fs = require('fs');
const https = require('https');
const http = require('http');
const { URL } = require('url');
const path = require('path');

class ApiClient {
    constructor(baseUrl) {
        this.baseUrl = baseUrl;
        this.token = null;
    }
    
    setAuthorization(token) {
        this.token = token;
    }
    
    async get(endpoint, options = {}) {
        const url = this.baseUrl + endpoint;
        return this.makeRequest(url, 'GET', null, options);
    }
    
    async post(endpoint, data = null, options = {}) {
        const url = this.baseUrl + endpoint;
        return this.makeRequest(url, 'POST', data, options);
    }
    
    async postFile(endpoint, filePath, options = {}) {
        const url = this.baseUrl + endpoint;
        return this.makeFileRequest(url, 'POST', filePath, options);
    }
    
    async makeRequest(url, method, data, options = {}) {
        return new Promise((resolve, reject) => {
            const urlObj = new URL(url);
            const isHttps = urlObj.protocol === 'https:';
            const client = isHttps ? https : http;
            
            const headers = {
                'Content-Type': 'application/json',
                ...options.headers
            };
            
            if (this.token) {
                headers['x-authorization'] = `Bearer ${this.token}`;
            }
            
            const requestOptions = {
                hostname: urlObj.hostname,
                port: urlObj.port || (isHttps ? 443 : 80),
                path: urlObj.pathname + urlObj.search,
                method: method,
                headers: headers
            };
            
            const req = client.request(requestOptions, (res) => {
                let responseData = '';
                
                res.on('data', (chunk) => {
                    responseData += chunk;
                });
                
                res.on('end', () => {
                    resolve({
                        statusCode: res.statusCode,
                        headers: res.headers,
                        body: responseData
                    });
                });
            });
            
            req.on('error', (error) => {
                reject(error);
            });
            
            if (data) {
                req.write(JSON.stringify(data));
            }
            
            req.end();
        });
    }
    
    async makeFileRequest(url, method, filePath, options = {}) {
        return new Promise((resolve, reject) => {
            if (!fs.existsSync(filePath)) {
                reject(new Error(`File not found: ${filePath}`));
                return;
            }
            
            const urlObj = new URL(url);
            const isHttps = urlObj.protocol === 'https:';
            const client = isHttps ? https : http;
            
            const boundary = '----WebKitFormBoundary' + Math.random().toString(16).substr(2);
            const headers = {
                'Content-Type': `multipart/form-data; boundary=${boundary}`,
                ...options.headers
            };
            
            if (this.token) {
                headers['x-authorization'] = `Bearer ${this.token}`;
            }
            
            const requestOptions = {
                hostname: urlObj.hostname,
                port: urlObj.port || (isHttps ? 443 : 80),
                path: urlObj.pathname + urlObj.search,
                method: method,
                headers: headers
            };
            
            const req = client.request(requestOptions, (res) => {
                let responseData = '';
                
                res.on('data', (chunk) => {
                    responseData += chunk;
                });
                
                res.on('end', () => {
                    resolve({
                        statusCode: res.statusCode,
                        headers: res.headers,
                        body: responseData
                    });
                });
            });
            
            req.on('error', (error) => {
                reject(error);
            });
            
            // Create multipart form data
            const fileContent = fs.readFileSync(filePath);
            const fileName = path.basename(filePath);
            
            const body = [
                `--${boundary}`,
                `Content-Disposition: form-data; name="file"; filename="${fileName}"`,
                'Content-Type: text/csv',
                '',
                fileContent.toString('binary'),
                `--${boundary}--`
            ].join('\r\n');
            
            req.write(body);
            req.end();
        });
    }
}

class ConfigLoader {
    constructor(configPath = "config.json") {
        this.configPath = configPath;
    }
    
    loadConfig() {
        try {
            const configContent = fs.readFileSync(this.configPath, 'utf8');
            const config = JSON.parse(configContent);
            return config;
        } catch (error) {
            if (error.code === 'ENOENT') {
                throw new Error(`Config file not found: ${this.configPath}`);
            } else if (error instanceof SyntaxError) {
                throw new Error(`Invalid JSON in config file: ${this.configPath}`);
            } else {
                throw error;
            }
        }
    }
}

async function main() {
    // Get config path and CSV file path from command line arguments
    const configPath = process.argv[2] || "config.json";
    const csvFilePath = process.argv[3] || "curtailment-template.csv";
    
    try {
        // Load configuration
        const configLoader = new ConfigLoader(configPath);
        const config = configLoader.loadConfig();
        
        // Log the config content
        console.log("Configuration loaded:");
        console.log(JSON.stringify(config, null, 2));
        
        // Initialize API client
        const apiClient = new ApiClient(config.api.baseUrl);
        
        // Login to get authorization token
        const loginData = {
            user: config.auth.user,
            pass: config.auth.password
        };
        
        const response = await apiClient.post("/Auth", loginData);
        
        if (response.statusCode === 200) {
            const loginResponse = JSON.parse(response.body);
            const token = loginResponse.token;
            
            if (token) {
                apiClient.setAuthorization(token);
                console.log("Successfully logged in and set authorization token");
                
                // Import curtailments from CSV
                console.log(`\nImporting curtailments from: ${csvFilePath}`);
                const importResponse = await apiClient.postFile("/curtailment/csv", csvFilePath);
                
                if (importResponse.statusCode === 200) {
                    const importResult = JSON.parse(importResponse.body);
                    console.log("Curtailment import completed:");
                    console.log(`Errors: ${importResult.errors?.length || 0}`);
                    console.log(`Schedules imported: ${importResult.schedules?.length || 0}`);
                    
                    if (importResult.errors && importResult.errors.length > 0) {
                        console.log("\nImport errors:");
                        importResult.errors.forEach(error => {
                            console.log(`  - ${error}`);
                        });
                    }
                } else {
                    console.log(`Curtailment import failed with status code: ${importResponse.statusCode}`);
                    console.log(`Response: ${importResponse.body}`);
                }
                
                // Get site information first
                console.log("\nFetching site information...");
                const sitesResponse = await apiClient.get("/asset/site");
                
                if (sitesResponse.statusCode === 200) {
                    const sitesData = JSON.parse(sitesResponse.body);
                    console.log(`Found ${sitesData.length} sites:`);
                    sitesData.forEach(site => {
                        console.log(`  - ${site.name} (ID: ${site.id})`);
                    });
                    
                    // Extract site IDs for curtailment retrieval
                    const siteIds = sitesData.map(site => site.id);
                    
                    // Retrieve curtailment schedules using site IDs
                    console.log("\nRetrieving curtailment schedules...");
                    const schedulePayload = {
                        pageSize: 50,
                        expanded: [],
                        sites: siteIds
                    };
                    
                    const scheduleResponse = await apiClient.post("/curtailment/by-site", schedulePayload);
                    
                    if (scheduleResponse.statusCode === 200) {
                        const scheduleResult = JSON.parse(scheduleResponse.body);
                        console.log("Curtailment schedules retrieved:");
                        console.log(JSON.stringify(scheduleResult, null, 2));
                    } else {
                        console.log(`Failed to retrieve schedules with status code: ${scheduleResponse.statusCode}`);
                        console.log(`Response: ${scheduleResponse.body}`);
                    }
                } else {
                    console.log(`Failed to fetch sites with status code: ${sitesResponse.statusCode}`);
                    console.log(`Response: ${sitesResponse.body}`);
                }
                
            } else {
                console.log("Login successful but no token received");
            }
        } else {
            console.log(`Login failed with status code: ${response.statusCode}`);
            console.log(`Response: ${response.body}`);
        }
        
    } catch (error) {
        console.error(`Error: ${error.message}`);
    }
}

// Run the main function
main(); 