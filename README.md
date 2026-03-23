# ARSOS API Examples

This repository contains practical examples demonstrating how to use the ARSOS API for various use cases. Each example is provided in multiple programming languages (PHP, Python, and JavaScript) to help developers integrate with the ARSOS platform regardless of their preferred technology stack.

## Purpose

The main goal of this repository is to:
- Provide working code examples for common ARSOS API use cases
- Demonstrate authentication and API client setup patterns
- Show how to handle different data formats (JSON, CSV)
- Offer cross-language implementations for better developer experience
- Serve as a reference for API integration best practices

## Available Use Cases

### 1. Curtailment Planner

**Description**: Demonstrates how to manage curtailment schedules for renewable energy sites, including importing schedules from CSV files and retrieving existing schedules.

**Features**:
- Load configuration from JSON files
- Authenticate with the ARSOS API
- Import curtailment schedules from CSV files
- Fetch site information from the API
- Retrieve curtailment schedules by site IDs
- Handle error responses and validation

## Getting Started

1. **Clone the repository**:
   ```bash
   git clone git@github.com:greeneaglesolutions/arsos-api-examples.git
   cd arsos-api-examples
   ```

2. **Choose your use case**:
   Navigate to the specific example directory (e.g., `curtailment-planner`)

3. **Select your language**:
   Each example provides implementations in:
   - **Python**: Using `requests` library
   - **PHP**: Using cURL extension
   - **JavaScript**: Using Node.js
   - **.NET**: Using C# with HttpClient

4. **Configure your environment**:
   - Copy the `config.template.json` to `config.json`
   - Update with your API credentials and base URL
   - Install language-specific dependencies

5. **Run the example**:
   Follow the language-specific instructions in each example's README

## Configuration

All examples expect a configuration file with the following structure:

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