@description('The primary Azure region for resources')
param location string

@description('The name of the project')
param projectName string

@description('The environment name')
param environment string

@description('Tags to apply to resources')
param tags object

var cosmosDbAccountName = '${projectName}-${environment}-cosmos'
var databaseName = 'indepth-dispenza'

resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: cosmosDbAccountName
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    enableFreeTier: true  // Always use free tier (400 RU/s, 25GB) - perfect for small projects
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    publicNetworkAccess: 'Enabled'
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosDbAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

// Containers will be created when needed (Story 1 & Story 2)
// Removed for now to simplify initial deployment

output accountName string = cosmosDbAccount.name
output accountId string = cosmosDbAccount.id
output endpoint string = cosmosDbAccount.properties.documentEndpoint
output databaseName string = database.name
