# AGC Status Request Example

This directory contains example scripts in four programming languages (Python, PHP, JavaScript, and .NET/C#) that demonstrate how to interact with the ARSOS AGC Status Request API.

The scripts show how to:

1. Load configuration from a JSON file
2. Create an API client with authentication
3. Login to the ARSOS API
4. Upload an AGC status request XML file (96 quarter-hourly On/Off values per UPROG)
5. Query the status of all UPROGs for a given gas day and quarter-hourly position
6. Query the status of a specific UPROG for a given gas day and quarter-hourly position

## Structure

```
├── python/
│   ├── main.py
│   └── requirements.txt
├── php/
│   └── main.php
├── javascript/
│   ├── main.js
│   └── package.json
├── dotnet/
│   ├── Program.cs
│   └── RegulationFilesReader.csproj
├── config.template.json
├── agc-status-request-sample.xml
├── swagger-agc-status-request.yaml
└── README.md
```

## Configuration

All scripts expect a `config.json` file with the following structure:

```json
{
  "api": {
    "baseUrl": "https://api.greeneaglesolutions.com"
  },
  "auth": {
    "user": "your-user@company.com",
    "password": "your-password-here"
  }
}
```

Copy `config.template.json` to `config.json` and fill in your credentials.

## XML Format for Upload

Each XML file must contain one or more `SeriesTemporales` blocks — one per UPROG. Each block must include exactly **96 `Intervalo` entries** (one per quarter-hour of the gas day).

```xml
<AgcStatusRequest>
  <SeriesTemporales>
    <UPEntrada v="UPROG Danubio" />
    <Periodo>
      <IntervaloTiempo v="2026-04-26T22:00Z/2026-04-27T22:00Z" />
      <Resolucion v="PT15M" />
      <Intervalo><Pos v="1" /><Ctd v="On" /></Intervalo>
      <Intervalo><Pos v="2" /><Ctd v="Off" /></Intervalo>
      <!-- ... up to position 96 -->
    </Periodo>
  </SeriesTemporales>
</AgcStatusRequest>
```

A fully populated sample file with two UPROGs is provided in `agc-status-request-sample.xml`.
You can also download a fresh copy directly from the API:

```
GET /arsos-regulation-files-reader/agc-status-request/sample
```

### Quarter-hourly positions

Each gas day starts at **22:00 UTC** of the previous calendar day and ends at **22:00 UTC** of the stated date. Position 1 = first 15-minute slot, position 96 = last.

| Position | UTC time (gas day 2026-04-27) |
|----------|-------------------------------|
| 1 | 2026-04-26T22:00Z – 2026-04-26T22:15Z |
| 4 | 2026-04-26T22:45Z – 2026-04-26T23:00Z |
| 96 | 2026-04-27T21:45Z – 2026-04-27T22:00Z |

## Usage

### Python

1. Install dependencies:
   ```bash
   cd python
   pip install -r requirements.txt
   ```

2. Run the script:
   ```bash
   python main.py [config_path] [xml_file_path]
   ```

### PHP

1. Ensure PHP and the cURL extension are installed
2. Run the script:
   ```bash
   php main.php [config_path] [xml_file_path]
   ```

### JavaScript (Node.js)

1. Ensure Node.js >= 14.0.0 is installed (no external packages needed)
2. Run the script:
   ```bash
   node main.js [config_path] [xml_file_path]
   ```

### .NET (C#)

1. Ensure .NET 8.0 SDK is installed
2. Run the script:
   ```bash
   cd dotnet
   dotnet run -- [config_path] [xml_file_path]
   ```

## Command Line Arguments

- **config_path** (optional): Path to the configuration JSON file (default: `config.json`)
- **xml_file_path** (optional): Path to the AGC status XML file (default: `agc-status-request-sample.xml`)

## Features

- **ApiClient**: Handles HTTP requests with automatic `X-Authorization: Bearer {token}` header injection
- **ConfigLoader**: Loads and validates JSON configuration files
- **Login**: Authenticates with the ARSOS API using the `/Auth` endpoint
- **Token Management**: Automatically sets the authorization token for subsequent requests
- **XML Upload**: Multipart file upload of AGC status request XML
- **Tenant Query**: Fetches status for all UPROGs of the authenticated tenant
- **Site Query**: Fetches status for a single UPROG by site path

## API Endpoints

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `/Auth` | No | Login — returns a JWT token |
| `GET` | `/arsos-regulation-files-reader/agc-status-request/sample` | No | Download the XML template |
| `POST` | `/arsos-regulation-files-reader/agc-status-request/upload` | Yes | Upload an AGC status XML file |
| `GET` | `/arsos-regulation-files-reader/agc-status-request/{date}/{quarterHourly}` | Yes | Get all UPROGs' status for a gas day + position |
| `GET` | `/arsos-regulation-files-reader/agc-status-request/{sitePath}/{date}/{quarterHourly}` | Yes | Get one UPROG's status |

Full OpenAPI specification: `swagger-agc-status-request.yaml`

### Upload Response

```json
{
  "processedCount": 2,
  "skippedCount": 0
}
```

- `processedCount`: UPROGs successfully saved
- `skippedCount`: UPROGs whose name was not found for your tenant (no data written)

### Query by Date + Quarter-Hourly Response

```json
[
  { "sitePath": "UPROG Danubio", "value": 1 },
  { "sitePath": "UPROG Manzanares", "value": -1 }
]
```

### Query by Site Path Response

Returns a single value: `1` (On), `-1` (Off), or `null` (no data for that interval).

### Value Interpretation

| Value | Meaning |
|-------|---------|
| `1` | UPROG status is **On** |
| `-1` | UPROG status is **Off** |
| `null` | No data uploaded for this interval |

## Workflow

The scripts follow this workflow:

1. **Configuration Loading**: Load API base URL and credentials from JSON file
2. **Authentication**: POST to `/Auth` and obtain JWT token
3. **XML Upload**: POST the XML file to `/arsos-regulation-files-reader/agc-status-request/upload`
4. **Tenant Query**: GET all UPROGs' status for a given date and quarter-hourly position
5. **Site Query**: GET a single UPROG's status for the same date and position

## Error Handling

The scripts include comprehensive error handling for:

- Missing or invalid configuration files
- Failed authentication
- File not found (XML input)
- Upload validation errors (empty file, wrong extension)
- UPROG not found (404)
- Network connectivity issues

## Example Output

```
Configuration loaded:
{
  "api": { "baseUrl": "https://api.greeneaglesolutions.com" },
  "auth": { "user": "user@company.com", "password": "***" }
}
Successfully logged in and set authorization token

Uploading AGC status request from: agc-status-request-sample.xml
Upload completed:
  Processed: 2 UPROGs
  Skipped:   0 UPROGs

Querying all UPROGs for 2026-04-27, quarter-hourly position 1...
Found 2 UPROG(s):
  - UPROG Danubio: On
  - UPROG Manzanares: On

Querying specific UPROG 'UPROG Danubio' for 2026-04-27, position 1...
  Status: On
```
