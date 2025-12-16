targetScope = 'subscription'

@description('The environment name (e.g., dev, prod, tmp, test, etc.)')
param environment string = 'dev'

@description('The primary Azure region for resources')
param location string = 'westeurope'

@description('The name of the project')
param projectName string = 'indepth-dispenza'

@description('Tags to apply to all resources')
param tags object = {
  Project: 'InDepth Dispenza'
  Environment: environment
  ManagedBy: 'Bicep'
}

@description('Azure OpenAI API Key')
@secure()
param azureOpenAiApiKey string

@description('Azure OpenAI Endpoint')
param azureOpenAiEndpoint string = ''

@description('YouTube API Key')
@secure()
param youTubeApiKey string

@description('YouTube Transcript API Token')
@secure()
param youTubeTranscriptApiToken string

// Create resource group
resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: '${projectName}-${environment}-rg'
  location: location
  tags: tags
}

// Deploy Application Insights
module appInsights 'modules/app-insights.bicep' = {
  name: 'appInsights-deployment'
  scope: rg
  params: {
    location: location
    projectName: projectName
    environment: environment
    tags: tags
  }
}

// Deploy Storage Account
module storage 'modules/storage.bicep' = {
  name: 'storage-deployment'
  scope: rg
  params: {
    location: location
    projectName: projectName
    environment: environment
    tags: tags
  }
}

// Deploy Cosmos DB
module cosmosDb 'modules/cosmos-db.bicep' = {
  name: 'cosmosdb-deployment'
  scope: rg
  params: {
    location: location
    projectName: projectName
    environment: environment
    tags: tags
  }
}

// Deploy Function App
module functionApp 'modules/function-app.bicep' = {
  name: 'functionapp-deployment'
  scope: rg
  params: {
    location: location
    projectName: projectName
    environment: environment
    tags: tags
    storageAccountName: storage.outputs.storageAccountName
    appInsightsInstrumentationKey: appInsights.outputs.instrumentationKey
    appInsightsConnectionString: appInsights.outputs.connectionString
    cosmosDbAccountName: cosmosDb.outputs.accountName
    cosmosDbEndpoint: cosmosDb.outputs.endpoint
    cosmosDbAccountKey: cosmosDb.outputs.accountKey
    azureOpenAiApiKey: azureOpenAiApiKey
    azureOpenAiEndpoint: azureOpenAiEndpoint
    youTubeApiKey: youTubeApiKey
    youTubeTranscriptApiToken: youTubeTranscriptApiToken
  }
}

// Outputs for CI/CD pipeline
output resourceGroupName string = rg.name
output functionAppName string = functionApp.outputs.functionAppName
output storageAccountName string = storage.outputs.storageAccountName
output cosmosDbAccountName string = cosmosDb.outputs.accountName
output appInsightsName string = appInsights.outputs.appInsightsName
