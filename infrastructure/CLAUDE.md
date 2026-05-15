# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Validate Bicep templates
az bicep build --file main.bicep

# Preview changes (what-if)
az deployment sub what-if \
  --location westeurope \
  --template-file main.bicep \
  --parameters parameters/dev.parameters.json

# Deploy to an environment
az deployment sub create \
  --location westeurope \
  --template-file main.bicep \
  --parameters parameters/dev.parameters.json

# View deployment outputs
az deployment sub show --name <deployment-name> --query properties.outputs

# Delete resource group
az group delete --name atlas-of-alchemy-dev-rg --yes --no-wait
```

## Structure

- `main.bicep` - Main orchestration file (subscription-scoped deployment)
- `modules/` - Resource-specific Bicep modules
- `parameters/` - Environment-specific parameter files (dev, prod, tmp)

## Module Architecture

`main.bicep` orchestrates these modules:
1. **app-insights.bicep** - Application Insights + Log Analytics
2. **storage.bicep** - Storage Account with queues
3. **cosmos-db.bicep** - Cosmos DB (serverless, free tier)
4. **function-app.bicep** - Azure Functions (Linux consumption plan, .NET 9)

Azure OpenAI module exists but is disabled due to regional SKU issues - using external Grok API instead.

## Naming Convention

Resources follow `{projectName}-{environment}-{suffix}` pattern:
- Resource Group: `atlas-of-alchemy-{env}-rg`
- Function App: `atlas-of-alchemy-{env}-func`
- Cosmos DB: `atlas-of-alchemy-{env}-cosmos`
- Storage: `atlasofalchemy{env}st` (no hyphens, storage naming rules)

## CI/CD

GitHub Actions workflow (`.github/workflows/infrastructure.yml`) handles deployment:
- Triggers on push to `main` when `infrastructure/**` changes
- Validates commits don't mix infrastructure and code changes
- Requires `AZURE_CREDENTIALS` and `AZURE_SUBSCRIPTION_ID` secrets

## Secrets

Sensitive parameters passed via GitHub secrets to Bicep:
- `youTubeApiKey`
- `youTubeTranscriptApiToken`
- `grokApiKey`

See README.md for detailed CI/CD setup and troubleshooting.