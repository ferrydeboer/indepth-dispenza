@description('The primary Azure region for resources')
param location string

@description('The name of the project')
param projectName string

@description('The environment name')
param environment string

@description('Tags to apply to resources')
param tags object

// Storage account names must be globally unique and lowercase with no special characters
var storageAccountName = toLower(replace('${projectName}${environment}st', '-', ''))

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

// Queue service for video analysis pipeline
resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

// Queue for transcript analysis jobs
resource transcriptAnalysisQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: 'transcript-analysis'
  properties: {
    metadata: {
      description: 'Queue for triggering transcript analysis jobs'
    }
  }
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
output queueEndpoint string = storageAccount.properties.primaryEndpoints.queue
