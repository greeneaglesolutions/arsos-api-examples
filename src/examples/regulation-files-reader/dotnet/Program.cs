using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

class ApiClient
{
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;
    private string? _token;

    public ApiClient(string baseUrl)
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient();
    }

    public void SetAuthorization(string token)
    {
        _token = token;
    }

    public async Task<ApiResponse> GetAsync(string endpoint)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + endpoint);
        ApplyHeaders(request);

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        return new ApiResponse
        {
            StatusCode = (int)response.StatusCode,
            Body = body
        };
    }

    public async Task<ApiResponse> PostAsync(string endpoint, object? data = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + endpoint);
        ApplyHeaders(request);

        if (data != null)
        {
            var json = JsonSerializer.Serialize(data);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        return new ApiResponse
        {
            StatusCode = (int)response.StatusCode,
            Body = body
        };
    }

    public async Task<ApiResponse> PostFileAsync(string endpoint, string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + endpoint);

        if (_token != null)
            request.Headers.Add("Authorization", $"Bearer {_token}");

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        content.Add(fileContent, "file", Path.GetFileName(filePath));
        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        return new ApiResponse
        {
            StatusCode = (int)response.StatusCode,
            Body = body
        };
    }

    private void ApplyHeaders(HttpRequestMessage request)
    {
        if (_token != null)
            request.Headers.Add("Authorization", $"Bearer {_token}");
    }
}

class ApiResponse
{
    public int StatusCode { get; set; }
    public string Body { get; set; } = string.Empty;
}

class ConfigLoader
{
    private readonly string _configPath;

    public ConfigLoader(string configPath = "config.json")
    {
        _configPath = configPath;
    }

    public JsonElement LoadConfig()
    {
        if (!File.Exists(_configPath))
            throw new FileNotFoundException($"Config file not found: {_configPath}");

        try
        {
            var configContent = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<JsonElement>(configContent);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Invalid JSON in config file: {_configPath}");
        }
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var configPath = args.Length > 0 ? args[0] : "config.json";
        var xmlFilePath = args.Length > 1 ? args[1] : "agc-status-request-sample.xml";

        try
        {
            var configLoader = new ConfigLoader(configPath);
            var config = configLoader.LoadConfig();

            Console.WriteLine("Configuration loaded:");
            Console.WriteLine(JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

            var baseUrl = config.GetProperty("api").GetProperty("baseUrl").GetString()!;
            var apiClient = new ApiClient(baseUrl);

            // Authenticate
            var loginData = new
            {
                user = config.GetProperty("auth").GetProperty("user").GetString(),
                pass = config.GetProperty("auth").GetProperty("password").GetString()
            };

            var response = await apiClient.PostAsync("/Auth", loginData);

            if (response.StatusCode != 200)
            {
                Console.WriteLine($"Login failed with status code: {response.StatusCode}");
                Console.WriteLine($"Response: {response.Body}");
                return;
            }

            var loginResponse = JsonSerializer.Deserialize<JsonElement>(response.Body);
            if (!loginResponse.TryGetProperty("token", out var tokenElement))
            {
                Console.WriteLine("Login successful but no token received");
                return;
            }

            apiClient.SetAuthorization(tokenElement.GetString()!);
            Console.WriteLine("Successfully logged in and set authorization token");

            // Upload XML file
            Console.WriteLine($"\nUploading AGC status request from: {xmlFilePath}");
            var uploadResponse = await apiClient.PostFileAsync("/arsos-regulation-files-reader/agc-status-request/upload", xmlFilePath);

            if (uploadResponse.StatusCode == 200)
            {
                var uploadResult = JsonSerializer.Deserialize<JsonElement>(uploadResponse.Body);
                var processed = uploadResult.TryGetProperty("processedCount", out var pc) ? pc.GetInt32() : 0;
                var skipped = uploadResult.TryGetProperty("skippedCount", out var sc) ? sc.GetInt32() : 0;
                Console.WriteLine("Upload completed:");
                Console.WriteLine($"  Processed: {processed} UPROGs");
                Console.WriteLine($"  Skipped:   {skipped} UPROGs");
            }
            else
            {
                Console.WriteLine($"Upload failed with status code: {uploadResponse.StatusCode}");
                Console.WriteLine($"Response: {uploadResponse.Body}");
                return;
            }

            // Query all UPROGs for a given gas day and quarter-hourly position
            var date = "2026-04-27";
            var quarterHourly = 1;
            Console.WriteLine($"\nQuerying all UPROGs for {date}, quarter-hourly position {quarterHourly}...");
            var allResponse = await apiClient.GetAsync($"/arsos-regulation-files-reader/agc-status-request/{date}/{quarterHourly}");

            if (allResponse.StatusCode == 200)
            {
                var values = JsonSerializer.Deserialize<JsonElement>(allResponse.Body);
                var entries = values.EnumerateArray();
                Console.WriteLine($"Found UPROG(s):");
                foreach (var entry in entries)
                {
                    var sitePath = entry.GetProperty("sitePath").GetString();
                    var value = entry.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null
                        ? v.GetInt32() : (int?)null;
                    var status = value == 1 ? "On" : value == -1 ? "Off" : "No data";
                    Console.WriteLine($"  - {sitePath}: {status}");
                }
            }
            else
            {
                Console.WriteLine($"Query failed with status code: {allResponse.StatusCode}");
                Console.WriteLine($"Response: {allResponse.Body}");
            }

            // Query a specific UPROG
            var siteName = "UPROG Danubio";
            Console.WriteLine($"\nQuerying specific UPROG '{siteName}' for {date}, position {quarterHourly}...");
            var siteResponse = await apiClient.GetAsync(
                $"/arsos-regulation-files-reader/agc-status-request/{Uri.EscapeDataString(siteName)}/{date}/{quarterHourly}"
            );

            if (siteResponse.StatusCode == 200)
            {
                var siteValue = JsonSerializer.Deserialize<JsonElement>(siteResponse.Body);
                var value = siteValue.ValueKind != JsonValueKind.Null ? siteValue.GetInt32() : (int?)null;
                var status = value == 1 ? "On" : value == -1 ? "Off" : "No data";
                Console.WriteLine($"  Status: {status}");
            }
            else if (siteResponse.StatusCode == 404)
            {
                Console.WriteLine($"  UPROG not found: {siteResponse.Body}");
            }
            else
            {
                Console.WriteLine($"Query failed with status code: {siteResponse.StatusCode}");
                Console.WriteLine($"Response: {siteResponse.Body}");
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
        }
    }
}
