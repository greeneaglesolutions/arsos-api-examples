# Curtailment Planner Example

This directory contains example scripts in three programming languages (Python, PHP, and JavaScript) that demonstrate how to interact with ARSOS Curtailment planner API

1. Load configuration from a JSON file
2. Create an API client with authentication
3. Login to the ARSOS API
4. Set authorization tokens for subsequent API calls
5. Import curtailment schedules from CSV files
6. Fetch site information from the API
7. Retrieve curtailment schedules from the API using site IDs

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
├── config.json
├── curtailment-template.csv
└── README.md
```

## Configuration

All scripts expect a `config.json` file with the following structure:

```json
{
  "api": {
    "baseUrl": "https://your-api-url.com"
  },
  "auth": {
    "user": "your_username",
    "password": "your_password"
  }
}
```

## CSV Format for Curtailment Import

The scripts expect a CSV file with the following columns:

```csv
site;startsAt (yyyy/mm/dd hh:mm);endAt (yyyy/mm/dd hh:mm);power (mw)
SITE001,2024-01-15T10:00:00Z,2024-01-15T12:00:00Z,25.5,Maintenance
SITE002,2024-01-16T14:00:00Z,2024-01-16T16:00:00Z,15.0,Grid stability
```

## Usage

### Python

1. Install dependencies:
   ```bash
   cd python
   pip install -r requirements.txt
   ```

2. Run the script:
   ```bash
   python main.py [config_path] [csv_file_path]
   ```

### PHP

1. Ensure PHP and cURL extension are installed
2. Run the script:
   ```bash
   php main.php [config_path] [csv_file_path]
   ```

### JavaScript (Node.js)

1. Ensure Node.js is installed
2. Run the script:
   ```bash
   node main.js [config_path] [csv_file_path]
   ```

## Command Line Arguments

- **config_path** (optional): Path to the configuration JSON file (default: `config.json`)
- **csv_file_path** (optional): Path to the CSV file for curtailment import (default: `curtailment-template.csv`)

## Features

- **ApiClient**: Handles HTTP requests with automatic authorization header injection
- **ConfigLoader**: Loads and validates JSON configuration files
- **Login**: Authenticates with the ARSOS API using `/Auth` endpoint
- **Token Management**: Automatically sets authorization tokens for subsequent requests
- **File Upload**: Supports CSV file upload for curtailment import
- **Site Discovery**: Fetches available sites from the API
- **Schedule Retrieval**: Fetches curtailment schedules from the API using site IDs

## API Endpoints

- `POST /Auth`: Login endpoint that returns a token
- `POST /curtailment/csv`: Import curtailment schedules from CSV file
- `GET /asset/site`: Retrieve list of available sites
- `POST /curtailment/by-site`: Retrieve curtailment schedules by site

### Site Information Response Format

```json
[
  {
    "id": "site_internal_id",
    "name": "Site Display Name"
  }
]
```

### Import Response Format

```json
{
  "errors": [
    "Error message for failed import"
  ],
  "schedules": [
    {
      "id": "schedule_id",
      "site_id": "SITE001",
      "start_time": "2024-01-15T10:00:00Z",
      "end_time": "2024-01-15T12:00:00Z",
      "power_reduction": 25.5,
      "reason": "Maintenance"
    }
  ]
}
```

### Schedule Retrieval Payload

```json
{
  "pageSize": 50,
  "expanded": [],
  "sites": ["site_id_1", "site_id_2"]
}
```

## Workflow

The scripts follow this workflow:

1. **Configuration Loading**: Load API configuration from JSON file
2. **Authentication**: Login to the ARSOS API and obtain authorization token
3. **CSV Import**: Upload curtailment schedules from CSV file
4. **Site Discovery**: Fetch available sites from `/asset/site` endpoint
5. **Schedule Retrieval**: Use site IDs to retrieve curtailment schedules

## Error Handling

The scripts include comprehensive error handling for:
- Missing configuration files
- Invalid JSON configuration
- File upload errors
- API authentication failures
- Network connectivity issues
- Site fetching failures

## Example Output

```
Configuration loaded:
{
  "api": {
    "baseUrl": "https://api.example.com"
  },
  "auth": {
    "user": "your_username",
    "password": "your_password"
  }
}
Successfully logged in and set authorization token

Importing curtailments from: curtailment-template.csv
Curtailment import completed:
Errors: 0
Schedules imported: 3

Fetching site information...
Found 5 sites:
  - Solar Farm Alpha (ID: SITE001)
  - Wind Farm Beta (ID: SITE002)
  - Hydro Plant Gamma (ID: SITE003)
  - Solar Farm Delta (ID: SITE004)
  - Wind Farm Epsilon (ID: SITE005)

Retrieving curtailment schedules...
Curtailment schedules retrieved:
{
  "schedules": [...],
  "total": 3,
  "page": 1
}
``` 