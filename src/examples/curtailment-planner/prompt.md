# Session Prompts

This file contains prompts to be executed during the current development session. 
Add new prompts below and indicate when they should be executed.

## Template for New Prompts

### Prompt 1: [Script boilerplate]
**Status:** [Completed]
**Description:** Basic structure for the script

```
I want a script that will use an ApiClient class that will expose three methods
- get
- post
- setAuthorization

Get and post will do http request to our API
Set authorization will receive a token and save it. If we have an authorization token, it will use in every http call of the ApiClient using a x-authorization header with Bearer $token as value

I also want a ConfigLoader class that will load a config json with the following structure:
{
    api: {
        baseUrl: string
    },
    auth: {
        user: string,
        password: string
    }
}

The main entry point for the script should be a main file that will load the config (by default it should look for a config.json file in the same folder as the main script, but it should accept the path as the first argument of the execution) and log the config content for now.
I want the examples to be created both in Python, PHP and Javascript. Create a folder with each programming language name and save the corresponding code inside each folder.
Every time we do a prompt iteration, update the three languages
```

### Prompt 2: [Login]
**Status:** [Completed]
**Description:** Login to ARSOS API

```
First we are going to login into the API.
For that, we need to make a POST API call to the path /Auth with payload:
{
    "user": $user,
    "pass": $pass
}
This will return a response like this: 
{
    "token": $token
}
We will use the returned token to authorize API calls using a x-authorization header with Bearer $token as value
```

### Prompt 3: [Import curtailments into ARSOS]
**Status:** [Completed]
**Description:** Stream curtailments from a CSV and import them into the API

```
To import curtailments into ARSOS API, we are going to make a POST API call to /curtailment/csv with a binary payload with the CSV file (by default it should look for a curtailment-template.csv in the same folder as the main script, but it should accept the path as the second argument of the execution)
Response looks like this:
{
    errors: [] one object per error importing the schedule (there can be more than one error per row)
    schedules: [] one object per imported schedule
}
```

### Prompt 4: [Retreiving curtailment schedules from ARSOS]
**Status:** [Completed]
**Description:** Get scheduled curtailments from ARSOS

```
To retreive curtailment schedules from ARSOS API we are going to make a POST API call to /curtailment/by-site with the following payload:
{
    pageSize: number of schedules returned per page
    expanded: [] Array of site paths (internal ARSOS id for the site, different from the beautiful name) we want to retreive the full schedule list from
    sites: [] Array of site paths (internal ARSOS id for the site, different from the beautiful name) we want to retreive schedules from
}
Before retreiving the curtailments, we are going to make a GET API call to /asset/site. Response looks like this:
[
    {
        "id": string,
        "name": string
    }
]
Map the response to an array of strings where each position is the id of the site, and use it to retreive the schedules
Echo in the terminal the JSON response.
All these scripts are meant to be given as examples to our users, so please include any relevant information about usage in the README
```

## Instructions for Use

1. Add new prompts below with clear descriptions
2. Mark status as "Ready to execute" when you want me to process a prompt
3. Mark status as "Completed" after execution
4. Use "Pending" for prompts that are not yet ready

## Template for New Prompts

### Prompt X: [Title]
**Status:** [Ready to execute | Pending | Completed]
**Description:** [Brief description of what this prompt should accomplish]

```
[Your prompt content here]
```
