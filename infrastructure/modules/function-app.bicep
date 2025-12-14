@description('The primary Azure region for resources')
param location string

@description('The name of the project')
param projectName string

@description('The environment name')
param environment string

@description('Tags to apply to resources')
param tags object

@description('Storage account name for function app')
param storageAccountName string

@description('Application Insights instrumentation key')
@secure()
param appInsightsInstrumentationKey string

@description('Application Insights connection string')
@secure()
param appInsightsConnectionString string

@description('Cosmos DB account name')
param cosmosDbAccountName string

@description('Cosmos DB endpoint')
param cosmosDbEndpoint string

var functionAppName = '${projectName}-${environment}-func'
var hostingPlanName = '${projectName}-${environment}-plan'

// Get reference to existing storage account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

// Cosmos DB reference removed - not needed until Story 1 implementation
// Will add role assignment when we actually use Cosmos DB

// Consumption plan for Azure Functions
// Note: Free subscriptions require Windows + specific regions (e.g., canadacentral)
resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: hostingPlanName
  location: location
  tags: tags
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: false  // Windows (not Linux) - required for free subscription
  }
}

// Function App
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp'  // Windows function app
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    reserved: false  // Windows
    httpsOnly: true
    siteConfig: {
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${az.environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'CosmosDb__AccountEndpoint'
          value: cosmosDbEndpoint
        }
        {
          name: 'CosmosDb__DatabaseName'
          value: 'indepth-dispenza'
        }
        {
          name: 'CosmosDb__TranscriptCacheContainer'
          value: 'transcript-cache'
        }
        {
          name: 'CosmosDb__VideoAnalysisContainer'
          value: 'video-analysis'
        }
      ]
    }
  }
}

// Separate config resource to set .NET framework version
// This is the proper way to configure runtime for Function Apps
resource functionAppConfig 'Microsoft.Web/sites/config@2023-12-01' = {
  name: 'web'
  parent: functionApp
  properties: {
    netFrameworkVersion: 'v9.0'
    ftpsState: 'Disabled'
    minTlsVersion: '1.2'
  }
}

// Cosmos DB role assignment removed for now - will add in Story 1 when actually needed
// Managed identity is still enabled on the Function App for future use

output functionAppName string = functionApp.name
output functionAppId string = functionApp.id
output functionAppPrincipalId string = functionApp.identity.principalId
