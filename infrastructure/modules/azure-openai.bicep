@description('The primary Azure region for resources')
param location string

@description('The name of the project')
param projectName string

@description('The environment name')
param environment string

@description('Tags to apply to resources')
param tags object

@description('The SKU of the Azure OpenAI service')
param sku string = 'S0'

@description('GPT model deployment name')
param gptDeploymentName string = 'gpt-4o-mini'

@description('GPT model name')
param gptModelName string = 'gpt-4o-mini'

@description('GPT model version')
param gptModelVersion string = '2024-07-18'

@description('GPT deployment capacity (in thousands of tokens per minute)')
param gptCapacity int = 50

var accountName = '${projectName}-${environment}-openai'

// Azure OpenAI Account
resource openAiAccount 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: accountName
  location: location
  tags: tags
  kind: 'OpenAI'
  sku: {
    name: sku
  }
  properties: {
    customSubDomainName: accountName
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

// Deploy GPT model with GlobalStandard SKU (pay-per-call, globally distributed)
resource gptDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAiAccount
  name: gptDeploymentName
  sku: {
    name: 'GlobalStandard'
    capacity: gptCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: gptModelName
      version: gptModelVersion
    }
  }
}

output accountName string = openAiAccount.name
output endpoint string = openAiAccount.properties.endpoint
output accountId string = openAiAccount.id
output deploymentName string = gptDeployment.name
