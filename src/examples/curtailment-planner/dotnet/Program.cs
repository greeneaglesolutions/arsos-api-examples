using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class ApiClient
{
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;
    private string _token;

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

    public async Task<ApiResponse> PostAsync(string endpoint, object data = null)
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
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + endpoint);

        if (_token != null)
        {
            request.Headers.Add("x-authorization", $"Bearer {_token}");
        }

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
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
        {
            request.Headers.Add("x-authorization", $"Bearer {_token}");
        }
    }
}

class ApiResponse
{
    public int StatusCode { get; set; }
    public string Body { get; set; }
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
        {
            throw new FileNotFoundException($"Config file not found: {_configPath}");
        }

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
        // Get config path and CSV file path from command line arguments
        var configPath = args.Length > 0 ? args[0] : "config.json";
        var csvFilePath = args.Length > 1 ? args[1] : "curtailment-template.csv";

        try
        {
            // Load configuration
            var configLoader = new ConfigLoader(configPath);
            var config = configLoader.LoadConfig();

            // Log the config content
            Console.WriteLine("Configuration loaded:");
            Console.WriteLine(JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

            // Initialize API client
            var baseUrl = config.GetProperty("api").GetProperty("baseUrl").GetString();
            var apiClient = new ApiClient(baseUrl);

            // Login to get authorization token
            var loginData = new
            {
                user = config.GetProperty("auth").GetProperty("user").GetString(),
                pass = config.GetProperty("auth").GetProperty("password").GetString()
            };

            var response = await apiClient.PostAsync("/Auth", loginData);

            if (response.StatusCode == 200)
            {
                var loginResponse = JsonSerializer.Deserialize<JsonElement>(response.Body);

                if (loginResponse.TryGetProperty("token", out var tokenElement))
                {
                    var token = tokenElement.GetString();
                    apiClient.SetAuthorization(token);
                    Console.WriteLine("Successfully logged in and set authorization token");

                    // Import curtailments from CSV
                    Console.WriteLine($"\nImporting curtailments from: {csvFilePath}");
                    var importResponse = await apiClient.PostFileAsync("/curtailment/csv", csvFilePath);

                    if (importResponse.StatusCode == 200)
                    {
                        var importResult = JsonSerializer.Deserialize<JsonElement>(importResponse.Body);
                        var errors = importResult.TryGetProperty("errors", out var errorsElement)
                            ? errorsElement.GetArrayLength() : 0;
                        var schedules = importResult.TryGetProperty("schedules", out var schedulesElement)
                            ? schedulesElement.GetArrayLength() : 0;

                        Console.WriteLine("Curtailment import completed:");
                        Console.WriteLine($"Errors: {errors}");
                        Console.WriteLine($"Schedules imported: {schedules}");

                        if (errors > 0)
                        {
                            Console.WriteLine("\nImport errors:");
                            foreach (var error in errorsElement.EnumerateArray())
                            {
                                Console.WriteLine($"  - {error.GetString()}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Curtailment import failed with status code: {importResponse.StatusCode}");
                        Console.WriteLine($"Response: {importResponse.Body}");
                    }

                    // Get site information first
                    Console.WriteLine("\nFetching site information...");
                    var sitesResponse = await apiClient.GetAsync("/asset/site");

                    if (sitesResponse.StatusCode == 200)
                    {
                        var sitesData = JsonSerializer.Deserialize<JsonElement>(sitesResponse.Body);
                        var sites = sitesData.EnumerateArray().ToList();
                        Console.WriteLine($"Found {sites.Count} sites:");

                        var siteIds = new List<string>();
                        foreach (var site in sites)
                        {
                            var name = site.GetProperty("name").GetString();
                            var id = site.GetProperty("id").GetString();
                            Console.WriteLine($"  - {name} (ID: {id})");
                            siteIds.Add(id);
                        }

                        // Retrieve curtailment schedules using site IDs
                        Console.WriteLine("\nRetrieving curtailment schedules...");
                        var schedulePayload = new
                        {
                            pageSize = 50,
                            expanded = Array.Empty<string>(),
                            sites = siteIds
                        };

                        var scheduleResponse = await apiClient.PostAsync("/curtailment/by-site", schedulePayload);

                        if (scheduleResponse.StatusCode == 200)
                        {
                            var scheduleResult = JsonSerializer.Deserialize<JsonElement>(scheduleResponse.Body);
                            Console.WriteLine("Curtailment schedules retrieved:");
                            Console.WriteLine(JsonSerializer.Serialize(scheduleResult, new JsonSerializerOptions { WriteIndented = true }));
                        }
                        else
                        {
                            Console.WriteLine($"Failed to retrieve schedules with status code: {scheduleResponse.StatusCode}");
                            Console.WriteLine($"Response: {scheduleResponse.Body}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to fetch sites with status code: {sitesResponse.StatusCode}");
                        Console.WriteLine($"Response: {sitesResponse.Body}");
                    }
                }
                else
                {
                    Console.WriteLine("Login successful but no token received");
                }
            }
            else
            {
                Console.WriteLine($"Login failed with status code: {response.StatusCode}");
                Console.WriteLine($"Response: {response.Body}");
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
        }
    }
}
