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
                headers['Authorization'] = `Bearer ${this.token}`;
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
                res.on('data', (chunk) => { responseData += chunk; });
                res.on('end', () => {
                    resolve({ statusCode: res.statusCode, headers: res.headers, body: responseData });
                });
            });

            req.on('error', (error) => { reject(error); });

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
                headers['Authorization'] = `Bearer ${this.token}`;
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
                res.on('data', (chunk) => { responseData += chunk; });
                res.on('end', () => {
                    resolve({ statusCode: res.statusCode, headers: res.headers, body: responseData });
                });
            });

            req.on('error', (error) => { reject(error); });

            const fileContent = fs.readFileSync(filePath);
            const fileName = path.basename(filePath);

            const body = [
                `--${boundary}`,
                `Content-Disposition: form-data; name="file"; filename="${fileName}"`,
                'Content-Type: application/xml',
                '',
                fileContent.toString('utf8'),
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
            return JSON.parse(configContent);
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
    const configPath = process.argv[2] || "config.json";
    const xmlFilePath = process.argv[3] || "agc-status-request-sample.xml";

    try {
        const configLoader = new ConfigLoader(configPath);
        const config = configLoader.loadConfig();

        console.log("Configuration loaded:");
        console.log(JSON.stringify(config, null, 2));

        const apiClient = new ApiClient(config.api.baseUrl);

        // Authenticate
        const loginData = {
            user: config.auth.user,
            pass: config.auth.password
        };

        const response = await apiClient.post("/Auth", loginData);

        if (response.statusCode !== 200) {
            console.log(`Login failed with status code: ${response.statusCode}`);
            console.log(`Response: ${response.body}`);
            return;
        }

        const loginResponse = JSON.parse(response.body);
        const token = loginResponse.token;

        if (!token) {
            console.log("Login successful but no token received");
            return;
        }

        apiClient.setAuthorization(token);
        console.log("Successfully logged in and set authorization token");

        // Upload XML file
        console.log(`\nUploading AGC status request from: ${xmlFilePath}`);
        const uploadResponse = await apiClient.postFile("/api/agc-status-request/upload", xmlFilePath);

        if (uploadResponse.statusCode === 200) {
            const uploadResult = JSON.parse(uploadResponse.body);
            console.log("Upload completed:");
            console.log(`  Processed: ${uploadResult.processedCount ?? 0} UPROGs`);
            console.log(`  Skipped:   ${uploadResult.skippedCount ?? 0} UPROGs`);
        } else {
            console.log(`Upload failed with status code: ${uploadResponse.statusCode}`);
            console.log(`Response: ${uploadResponse.body}`);
            return;
        }

        // Query all UPROGs for a given gas day and quarter-hourly position
        const date = "2026-04-27";
        const quarterHourly = 1;
        console.log(`\nQuerying all UPROGs for ${date}, quarter-hourly position ${quarterHourly}...`);
        const allResponse = await apiClient.get(`/api/agc-status-request/${date}/${quarterHourly}`);

        if (allResponse.statusCode === 200) {
            const values = JSON.parse(allResponse.body);
            console.log(`Found ${values.length} UPROG(s):`);
            values.forEach(entry => {
                const status = entry.value === 1 ? "On" : entry.value === -1 ? "Off" : "No data";
                console.log(`  - ${entry.sitePath}: ${status}`);
            });
        } else {
            console.log(`Query failed with status code: ${allResponse.statusCode}`);
            console.log(`Response: ${allResponse.body}`);
        }

        // Query a specific UPROG
        const sitePath = "UPROG Danubio";
        console.log(`\nQuerying specific UPROG '${sitePath}' for ${date}, position ${quarterHourly}...`);
        const siteResponse = await apiClient.get(
            `/api/agc-status-request/${encodeURIComponent(sitePath)}/${date}/${quarterHourly}`
        );

        if (siteResponse.statusCode === 200) {
            const value = JSON.parse(siteResponse.body);
            const status = value === 1 ? "On" : value === -1 ? "Off" : "No data";
            console.log(`  Status: ${status}`);
        } else if (siteResponse.statusCode === 404) {
            console.log(`  UPROG not found: ${siteResponse.body}`);
        } else {
            console.log(`Query failed with status code: ${siteResponse.statusCode}`);
            console.log(`Response: ${siteResponse.body}`);
        }

    } catch (error) {
        console.error(`Error: ${error.message}`);
    }
}

main();
