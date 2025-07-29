import json
import sys
import requests
from typing import Optional


class ApiClient:
    def __init__(self, base_url: str):
        self.base_url = base_url
        self.token: Optional[str] = None
    
    def set_authorization(self, token: str):
        """Set authorization token for API calls"""
        self.token = token
    
    def get(self, endpoint: str, **kwargs):
        """Make GET request to API"""
        headers = kwargs.get('headers', {})
        if self.token:
            headers['x-authorization'] = f'Bearer {self.token}'
        
        url = f"{self.base_url}{endpoint}"
        response = requests.get(url, headers=headers, **kwargs)
        return response
    
    def post(self, endpoint: str, data=None, **kwargs):
        """Make POST request to API"""
        headers = kwargs.get('headers', {})
        if self.token:
            headers['x-authorization'] = f'Bearer {self.token}'
        
        url = f"{self.base_url}{endpoint}"
        response = requests.post(url, json=data, headers=headers, **kwargs)
        return response
    
    def post_file(self, endpoint: str, file_path: str, **kwargs):
        """Make POST request with file upload"""
        headers = kwargs.get('headers', {})
        if self.token:
            headers['x-authorization'] = f'Bearer {self.token}'
        
        url = f"{self.base_url}{endpoint}"
        
        with open(file_path, 'rb') as file:
            files = {'file': file}
            response = requests.post(url, files=files, headers=headers, **kwargs)
        
        return response


class ConfigLoader:
    def __init__(self, config_path: str = "config.json"):
        self.config_path = config_path
    
    def load_config(self) -> dict:
        """Load configuration from JSON file"""
        try:
            with open(self.config_path, 'r') as file:
                config = json.load(file)
                return config
        except FileNotFoundError:
            raise FileNotFoundError(f"Config file not found: {self.config_path}")
        except json.JSONDecodeError:
            raise ValueError(f"Invalid JSON in config file: {self.config_path}")


def main():
    # Get config path and CSV file path from command line arguments
    config_path = sys.argv[1] if len(sys.argv) > 1 else "config.json"
    csv_file_path = sys.argv[2] if len(sys.argv) > 2 else "curtailment-template.csv"
    
    try:
        # Load configuration
        config_loader = ConfigLoader(config_path)
        config = config_loader.load_config()
        
        # Log the config content
        print("Configuration loaded:")
        print(json.dumps(config, indent=2))
        
        # Initialize API client
        api_client = ApiClient(config['api']['baseUrl'])
        
        # Login to get authorization token
        login_data = {
            "user": config['auth']['user'],
            "pass": config['auth']['password']
        }
        
        response = api_client.post("/Auth", data=login_data)
        
        if response.status_code == 200:
            login_response = response.json()
            token = login_response.get('token')
            if token:
                api_client.set_authorization(token)
                print("Successfully logged in and set authorization token")
                
                # Import curtailments from CSV
                print(f"\nImporting curtailments from: {csv_file_path}")
                import_response = api_client.post_file("/curtailment/csv", csv_file_path)
                
                if import_response.status_code == 200:
                    import_result = import_response.json()
                    print("Curtailment import completed:")
                    print(f"Errors: {len(import_result.get('errors', []))}")
                    print(f"Schedules imported: {len(import_result.get('schedules', []))}")
                    
                    if import_result.get('errors'):
                        print("\nImport errors:")
                        for error in import_result['errors']:
                            print(f"  - {error}")
                else:
                    print(f"Curtailment import failed with status code: {import_response.status_code}")
                    print(f"Response: {import_response.text}")
                
                # Get site information first
                print("\nFetching site information...")
                sites_response = api_client.get("/asset/site")
                
                if sites_response.status_code == 200:
                    sites_data = sites_response.json()
                    print(f"Found {len(sites_data)} sites:")
                    for site in sites_data:
                        print(f"  - {site['name']} (ID: {site['id']})")
                    
                    # Extract site IDs for curtailment retrieval
                    site_ids = [site['id'] for site in sites_data]
                    
                    # Retrieve curtailment schedules using site IDs
                    print("\nRetrieving curtailment schedules...")
                    schedule_payload = {
                        "pageSize": 50,
                        "expanded": [],
                        "sites": site_ids
                    }
                    
                    schedule_response = api_client.post("/curtailment/by-site", data=schedule_payload)
                    
                    if schedule_response.status_code == 200:
                        schedule_result = schedule_response.json()
                        print("Curtailment schedules retrieved:")
                        print(json.dumps(schedule_result, indent=2))
                    else:
                        print(f"Failed to retrieve schedules with status code: {schedule_response.status_code}")
                        print(f"Response: {schedule_response.text}")
                else:
                    print(f"Failed to fetch sites with status code: {sites_response.status_code}")
                    print(f"Response: {sites_response.text}")
                    
            else:
                print("Login successful but no token received")
        else:
            print(f"Login failed with status code: {response.status_code}")
            print(f"Response: {response.text}")
            
    except Exception as e:
        print(f"Error: {e}")


if __name__ == "__main__":
    main() 