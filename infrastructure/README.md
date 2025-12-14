# Infrastructure as Code (Bicep)

This directory contains Azure Bicep templates for provisioning the InDepth Dispenza infrastructure.

## Quick Start

### For CI/CD Deployment (GitHub Actions)
See [CI/CD Setup](#cicd-setup-github-actions) section below.

### For Manual Deployment
See [Deploy Infrastructure](#deploy-infrastructure) section below.

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

---

## CI/CD Setup (GitHub Actions)

The repository includes a GitHub Actions workflow (`.github/workflows/infrastructure.yml`) that automatically deploys infrastructure changes.

### Features

- ✅ **Automatic deployment** on push to `main` branch
- ✅ **Path filtering** - only triggers when `infrastructure/**` files change
- ✅ **Commit validation** - fails if a commit mixes infrastructure AND code changes (encourages small commits)
- ✅ **Bicep validation** - validates all templates before deployment
- ✅ **Manual trigger** - can be run manually via GitHub UI

### Prerequisites

Before the GitHub Actions workflow can deploy, you need to:

1. Create an Azure Service Principal
2. Configure GitHub Secrets

### Step 1: Create Azure Service Principal

Run these commands in Azure CLI:

```bash
# Set your subscription ID
SUBSCRIPTION_ID="your-subscription-id"
SUBSCRIPTION_NAME="your-subscription-name"

# Set subscription context
az account set --subscription $SUBSCRIPTION_ID

# Create a service principal with Contributor role at subscription level
az ad sp create-for-rbac \
  --name "indepth-dispenza-github-actions" \
  --role Contributor \
  --scopes /subscriptions/$SUBSCRIPTION_ID \
  --sdk-auth

# This will output JSON like:
# {
#   "clientId": "...",
#   "clientSecret": "...",
#   "subscriptionId": "...",
#   "tenantId": "...",
#   ...
# }
```

**⚠️ Important**: Copy the entire JSON output - you'll need it in the next step.

### Step 2: Configure GitHub Secrets

1. Go to your GitHub repository
2. Navigate to **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Add the following secrets:

| Secret Name | Value | Description |
|-------------|-------|-------------|
| `AZURE_CREDENTIALS` | The entire JSON output from step 1 | Service principal credentials |
| `AZURE_SUBSCRIPTION_ID` | Your Azure subscription ID | Used for portal links in workflow output |

**Example `AZURE_CREDENTIALS` secret:**
```json
{
  "clientId": "12345678-1234-1234-1234-123456789012",
  "clientSecret": "your-secret-here",
  "subscriptionId": "87654321-4321-4321-4321-210987654321",
  "tenantId": "abcdefgh-abcd-abcd-abcd-abcdefghijkl",
  "activeDirectoryEndpointUrl": "https://login.microsoftonline.com",
  "resourceManagerEndpointUrl": "https://management.azure.com/",
  "activeDirectoryGraphResourceId": "https://graph.windows.net/",
  "sqlManagementEndpointUrl": "https://management.core.windows.net:8443/",
  "galleryEndpointUrl": "https://gallery.azure.com/",
  "managementEndpointUrl": "https://management.core.windows.net/"
}
```

### Step 3: Test the Workflow

1. Make a change to any file in `infrastructure/` directory
2. Commit and push to `main` branch:
   ```bash
   git add infrastructure/
   git commit -m "test: trigger infrastructure deployment"
   git push origin main
   ```
3. Go to **Actions** tab in GitHub repository
4. Watch the `Deploy Infrastructure` workflow run

### Workflow Behavior

#### ✅ Will Deploy
- Push to `main` with only infrastructure changes
- Manual trigger via GitHub Actions UI

#### ✅ Will Validate (but not deploy)
- Pull request with infrastructure changes

#### ❌ Will Fail
- Commit contains BOTH infrastructure AND code changes
  ```bash
  # This will FAIL:
  git add infrastructure/main.bicep backend/SomeFunction.cs
  git commit -m "update infrastructure and code"
  ```

#### ⏭️ Will Skip
- Push to `main` with NO infrastructure changes
- Changes only to `backend/`, `frontend/`, `docs/`, etc.

### Workflow Jobs

1. **validate-commit**: Ensures commits don't mix infrastructure and code changes
2. **validate**: Runs `az bicep build` on all templates
3. **deploy**: Deploys to Azure (only on push to `main`, not PRs)

### Deployment Outputs

After successful deployment, the workflow provides:
- Resource Group name
- Function App name
- Direct link to Azure Portal

### Manual Deployment Trigger

You can also trigger deployment manually:

1. Go to **Actions** tab
2. Select **Deploy Infrastructure** workflow
3. Click **Run workflow**
4. Select branch and click **Run workflow**

### Troubleshooting CI/CD

#### "Azure Login Failed"
- Check that `AZURE_CREDENTIALS` secret is valid JSON
- Verify service principal has Contributor role
- Try recreating the service principal

#### "Insufficient Permissions"
- Service principal needs **Contributor** role at subscription level
- Run: `az role assignment list --assignee <clientId> --all`

#### "Deployment Failed"
- Check the workflow logs for detailed error messages
- Common issues:
  - Resource name conflicts (storage account names must be globally unique)
  - Quota limits (especially on free subscriptions)
  - Regional availability (some regions may not be available)

#### "Commit Validation Failed"
- Separate your commits: one for infrastructure, one for code
- Example:
  ```bash
  # First commit: infrastructure only
  git add infrastructure/
  git commit -m "feat(infra): add cosmos db containers"

  # Second commit: code only
  git add backend/
  git commit -m "feat: add transcript analysis function"
  ```

### Security Best Practices

- ✅ Service principal has minimum required permissions (Contributor at subscription level)
- ✅ Credentials stored as encrypted GitHub Secrets
- ✅ Workflow only runs on protected `main` branch
- ✅ No secrets exposed in logs or outputs

### Optional: Environment Protection

To add manual approval for deployments:

1. Go to **Settings** → **Environments**
2. Create environment named `tmp`
3. Add **Required reviewers** (your GitHub username)
4. Now deployments will wait for your approval

---

## Next Steps

After infrastructure is deployed:

1. Deploy Function App code via GitHub Actions (see `.github/workflows/dotnet.yml`)
2. Configure application secrets (YouTube API, Azure OpenAI) manually or via workflow
3. Test endpoints using Azure Portal or VS Code Azure Functions extension
