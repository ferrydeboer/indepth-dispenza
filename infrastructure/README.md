# Infrastructure as Code (Bicep)

This directory contains Azure Bicep templates for provisioning the InDepth Dispenza infrastructure.

## Structure

```
infrastructure/
├── main.bicep                      # Main orchestration file
├── modules/
│   ├── app-insights.bicep         # Application Insights + Log Analytics
│   ├── cosmos-db.bicep            # Cosmos DB (serverless) with containers
│   ├── function-app.bicep         # Azure Functions (consumption plan)
│   └── storage.bicep              # Storage Account with queues
├── parameters/
│   ├── dev.parameters.json        # Development environment
│   ├── prod.parameters.json       # Production environment
│   └── tmp.parameters.json        # Temporary/test environment
└── README.md
```

## Resources Provisioned

### Environment Support

The `environment` parameter accepts **any string value** (e.g., `dev`, `prod`, `tmp`, `test`, `yourname`).

This creates resources with that environment suffix:
- **Resource Group**: `indepth-dispenza-{environment}-rg`
- **Cosmos DB**: `indepth-dispenza-{environment}-cosmos`
- **Storage Account**: `indepthdispenza{environment}st`
- **Function App**: `indepth-dispenza-{environment}-func`
- **Application Insights**: `indepth-dispenza-{environment}-ai`

**All environments use the same configuration:**
- ✅ **Cosmos DB**: Serverless with **free tier** (400 RU/s, 25GB) - perfect for pet projects!
- ✅ **Storage Account**: Standard LRS with queue service
- ✅ **Function App**: Linux consumption plan (.NET 9 isolated)
- ✅ **Application Insights**: With Log Analytics workspace (30-day retention)

> **Note**: Use different environment names like `tmp`, `test`, or `yourname` to create temporary test environments without locking in dev/prod names.

## Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Bicep CLI](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/install) (or use `az bicep`)
- Azure subscription with appropriate permissions

## Validate Templates

```bash
# Validate main template
az bicep build --file main.bicep

# Validate all modules
cd modules
for file in *.bicep; do
  az bicep build --file "$file"
done
```

## Deploy Infrastructure

```bash
# Login to Azure
az login

# Set subscription (if you have multiple)
az account set --subscription "your-subscription-id"

# Deploy to dev environment
az deployment sub create \
  --location westeurope \
  --template-file main.bicep \
  --parameters parameters/dev.parameters.json

# Deploy to prod environment
az deployment sub create \
  --location westeurope \
  --template-file main.bicep \
  --parameters parameters/prod.parameters.json

# Deploy temporary test environment (won't block dev/prod names)
az deployment sub create \
  --location westeurope \
  --template-file main.bicep \
  --parameters parameters/tmp.parameters.json

# Or specify environment inline
az deployment sub create \
  --location westeurope \
  --template-file main.bicep \
  --parameters environment=test projectName=indepth-dispenza location=westeurope
```

## What-If Analysis

Preview changes before deploying:

```bash
az deployment sub what-if \
  --location westeurope \
  --template-file main.bicep \
  --parameters parameters/dev.parameters.json
```

## Outputs

The deployment outputs include:

- `resourceGroupName`: Name of the created resource group
- `functionAppName`: Name of the Function App
- `storageAccountName`: Name of the Storage Account
- `cosmosDbAccountName`: Name of the Cosmos DB account
- `appInsightsName`: Name of Application Insights

Access outputs after deployment:

```bash
az deployment sub show \
  --name <deployment-name> \
  --query properties.outputs
```

## Post-Deployment Configuration

### 1. Configure Function App Secrets

Some secrets need to be added manually (not included in IaC for security):

```bash
FUNCTION_APP_NAME="indepth-dispenza-dev-func"

# YouTube API Key
az functionapp config appsettings set \
  --name $FUNCTION_APP_NAME \
  --resource-group indepth-dispenza-dev-rg \
  --settings "YouTube__ApiKey=your-youtube-api-key"

# Azure OpenAI (for Story 2)
az functionapp config appsettings set \
  --name $FUNCTION_APP_NAME \
  --resource-group indepth-dispenza-dev-rg \
  --settings \
    "AzureOpenAI__Endpoint=https://your-resource.openai.azure.com/" \
    "AzureOpenAI__ApiKey=your-openai-key" \
    "AzureOpenAI__DeploymentName=gpt-4o-mini"
```

### 2. Get Cosmos DB Connection String

```bash
COSMOS_ACCOUNT_NAME="indepth-dispenza-dev-cosmos"
RESOURCE_GROUP="indepth-dispenza-dev-rg"

# Get primary key
az cosmosdb keys list \
  --name $COSMOS_ACCOUNT_NAME \
  --resource-group $RESOURCE_GROUP \
  --type keys \
  --query primaryMasterKey -o tsv
```

The Function App uses managed identity for Cosmos DB access (no connection string needed in production).

## Cost Estimates

### Development Environment (Monthly)
- **Cosmos DB Serverless**: Free tier (400 RU/s, 25GB) - $0
- **Storage Account**: Standard LRS - ~$0.01
- **Function App**: Consumption plan - Free tier (1M executions/month)
- **Application Insights**: 5GB free/month
- **Total**: ~$0-5/month

### Production Environment (Monthly)
- **Cosmos DB Serverless**: ~$5-10 (based on usage)
- **Storage Account**: ~$0.01-0.10
- **Function App**: ~$0.20/million executions after free tier
- **Application Insights**: $2.30/GB after free tier
- **Total**: ~$10-20/month (estimated for 500 videos)

## Clean Up Resources

To delete all resources:

```bash
az group delete --name indepth-dispenza-dev-rg --yes --no-wait
```

## Troubleshooting

### Deployment Fails with "Name Already Exists"

Storage account names must be globally unique. If deployment fails, modify `projectName` in parameters file:

```json
{
  "projectName": {
    "value": "indepth-dispenza-yourname"
  }
}
```

### Function App Not Starting

Check Application Insights logs or run:

```bash
az functionapp log tail \
  --name indepth-dispenza-dev-func \
  --resource-group indepth-dispenza-dev-rg
```

## Next Steps

After infrastructure is deployed:

1. Deploy Function App code via GitHub Actions (see `.github/workflows/`)
2. Configure application secrets (YouTube API, Azure OpenAI)
3. Test endpoints using Azure Portal or VS Code Azure Functions extension
