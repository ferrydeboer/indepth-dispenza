# Configuration Guide

## Overview

This project uses the standard .NET configuration approach with environment-specific settings files.

## Local Development

### Configuration Files

- `appsettings.json` - Common settings shared across all environments (committed to git)
- `appsettings.Development.json` - Local development settings with placeholder values (committed to git)
- `appsettings.Production.json` - Production logging overrides (committed to git)

### Setting up Local Development

1. Copy `appsettings.Development.json` to `appsettings.Development.local.json`
2. Update the values in `appsettings.Development.local.json` with your actual API keys
3. The `.local.json` file is gitignored and will override the checked-in Development settings

**Note:** The old `local.settings.json` approach is no longer used.

## Azure Deployment

### Required GitHub Secrets

Configure the following secrets in your GitHub repository for each environment (tmp, dev, prod):

#### Azure Infrastructure
- `AZURE_CREDENTIALS` - Azure service principal credentials
- `AZURE_SUBSCRIPTION_ID` - Your Azure subscription ID

#### API Keys
- `AZURE_OPENAI_API_KEY` - Your Azure OpenAI API key
- `AZURE_OPENAI_ENDPOINT` - Your Azure OpenAI endpoint URL (optional, can be empty string)
- `YOUTUBE_API_KEY` - Your YouTube Data API v3 key
- `YOUTUBE_TRANSCRIPT_API_TOKEN` - Your YouTube Transcript API token

### How It Works

1. GitHub Actions passes secrets as parameters to the Bicep deployment
2. Bicep provisions the Function App and configures app settings with the secrets
3. Function App reads configuration through standard .NET configuration system
4. Settings use double underscore notation (e.g., `CosmosDb__AccountKey`) in Azure app settings

### Configuration Hierarchy

Settings are loaded in this order (later overrides earlier):
1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Azure Function App Settings (when deployed)
4. `appsettings.{Environment}.local.json` (local only, gitignored)

## Configuration Structure

### CosmosDb
```json
{
  "CosmosDb": {
    "AccountEndpoint": "https://...",
    "AccountKey": "...",
    "DatabaseName": "indepth-dispenza",
    "TranscriptCacheContainer": "transcript-cache",
    "VideoAnalysisContainer": "video-analysis"
  }
}
```

### AzureOpenAI
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-openai-resource.openai.azure.com/",
    "ApiKey": "...",
    "DeploymentName": "gpt-4o-mini",
    "ApiVersion": "2024-02-01"
  }
}
```

### YouTube
```json
{
  "YouTube": {
    "ApiKey": "..."
  }
}
```

### YouTubeTranscriptApi
```json
{
  "YouTubeTranscriptApi": {
    "BaseUrl": "https://www.youtube-transcript.io",
    "ApiToken": "..."
  }
}
```

### VideoAnalysis
```json
{
  "VideoAnalysis": {
    "PreferredLanguages": ["en"]
  }
}
```
