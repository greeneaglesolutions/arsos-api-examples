import json
import sys
import requests
from typing import Optional


class ApiClient:
    def __init__(self, base_url: str):
        self.base_url = base_url
        self.token: Optional[str] = None

    def set_authorization(self, token: str):
        self.token = token

    def get(self, endpoint: str, **kwargs):
        headers = kwargs.get('headers', {})
        if self.token:
            headers['Authorization'] = f'Bearer {self.token}'
        url = f"{self.base_url}{endpoint}"
        response = requests.get(url, headers=headers, **kwargs)
        return response

    def post(self, endpoint: str, data=None, **kwargs):
        headers = kwargs.get('headers', {})
        if self.token:
            headers['Authorization'] = f'Bearer {self.token}'
        url = f"{self.base_url}{endpoint}"
        response = requests.post(url, json=data, headers=headers, **kwargs)
        return response

    def post_file(self, endpoint: str, file_path: str, **kwargs):
        headers = kwargs.get('headers', {})
        if self.token:
            headers['Authorization'] = f'Bearer {self.token}'
        url = f"{self.base_url}{endpoint}"
        with open(file_path, 'rb') as file:
            files = {'file': (file_path, file, 'application/xml')}
            response = requests.post(url, files=files, headers=headers, **kwargs)
        return response


class ConfigLoader:
    def __init__(self, config_path: str = "config.json"):
        self.config_path = config_path

    def load_config(self) -> dict:
        try:
            with open(self.config_path, 'r') as file:
                return json.load(file)
        except FileNotFoundError:
            raise FileNotFoundError(f"Config file not found: {self.config_path}")
        except json.JSONDecodeError:
            raise ValueError(f"Invalid JSON in config file: {self.config_path}")


def main():
    config_path = sys.argv[1] if len(sys.argv) > 1 else "config.json"
    xml_file_path = sys.argv[2] if len(sys.argv) > 2 else "agc-status-request-sample.xml"

    try:
        config_loader = ConfigLoader(config_path)
        config = config_loader.load_config()

        print("Configuration loaded:")
        print(json.dumps(config, indent=2))

        api_client = ApiClient(config['api']['baseUrl'])

        # Authenticate
        login_data = {
            "user": config['auth']['user'],
            "pass": config['auth']['password']
        }

        response = api_client.post("/Auth", data=login_data)

        if response.status_code != 200:
            print(f"Login failed with status code: {response.status_code}")
            print(f"Response: {response.text}")
            return

        token = response.json().get('token')
        if not token:
            print("Login successful but no token received")
            return

        api_client.set_authorization(token)
        print("Successfully logged in and set authorization token")

        # Upload XML file
        print(f"\nUploading AGC status request from: {xml_file_path}")
        upload_response = api_client.post_file(
            "/arsos-regulation-files-reader/agc-status-request/upload", xml_file_path
        )

        if upload_response.status_code == 200:
            upload_result = upload_response.json()
            print("Upload completed:")
            print(f"  Processed: {upload_result.get('processedCount', 0)} UPROGs")
            print(f"  Skipped:   {upload_result.get('skippedCount', 0)} UPROGs")
        else:
            print(f"Upload failed with status code: {upload_response.status_code}")
            print(f"Response: {upload_response.text}")
            return

        # Query all UPROGs for a given gas day and quarter-hourly position
        date = "2026-04-27"
        quarter_hourly = 1
        print(f"\nQuerying all UPROGs for {date}, quarter-hourly position {quarter_hourly}...")
        all_response = api_client.get(
            f"/arsos-regulation-files-reader/agc-status-request/{date}/{quarter_hourly}"
        )

        if all_response.status_code == 200:
            values = all_response.json()
            print(f"Found {len(values)} UPROG(s):")
            for entry in values:
                status = "On" if entry.get('value') == 1 else ("Off" if entry.get('value') == -1 else "No data")
                print(f"  - {entry.get('sitePath')}: {status}")
        else:
            print(f"Query failed with status code: {all_response.status_code}")
            print(f"Response: {all_response.text}")

        # Query a specific UPROG
        site_path = "UPROG Danubio"
        print(f"\nQuerying specific UPROG '{site_path}' for {date}, position {quarter_hourly}...")
        site_response = api_client.get(
            f"/arsos-regulation-files-reader/agc-status-request/{site_path}/{date}/{quarter_hourly}"
        )

        if site_response.status_code == 200:
            value = site_response.json()
            if value == 1:
                print(f"  Status: On")
            elif value == -1:
                print(f"  Status: Off")
            else:
                print(f"  Status: No data for this interval")
        elif site_response.status_code == 404:
            print(f"  UPROG not found: {site_response.text}")
        else:
            print(f"Query failed with status code: {site_response.status_code}")
            print(f"Response: {site_response.text}")

    except Exception as e:
        print(f"Error: {e}")


if __name__ == "__main__":
    main()
