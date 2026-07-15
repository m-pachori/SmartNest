// ============================================================
//  SmartNest - Function App Module (generic, reusable)
//  Consumption (Y1) plan, Windows, .NET 8 Isolated worker.
//  Parameterized by serviceName so Device/Identity/Automation/
//  Alert/Audit/Summary/Media services (Tasks 3-9) can reuse this
//  module without rework.
// ============================================================
@description('Azure region')
param location string

@description('Short service name used for naming, e.g. "home", "device"')
param serviceName string

@description('Function App name')
param functionAppName string

@description('App Service Plan (hosting plan) name')
param hostingPlanName string

@description('Key Vault secret URI for the storage account connection string (AzureWebJobsStorage)')
@secure()
param storageConnectionStringSecretUri string

@description('Cosmos DB endpoint (non-secret)')
param cosmosEndpoint string

@description('Cosmos DB database name')
param cosmosDatabaseName string

@description('Key Vault secret URI for the Cosmos DB primary key')
@secure()
param cosmosPrimaryKeySecretUri string

@description('Key Vault secret URI for the Service Bus connection string this service uses')
@secure()
param serviceBusConnectionStringSecretUri string

@description('Application Insights connection string')
@secure()
param appInsightsConnectionString string

@description('Additional, service-specific app settings (e.g. container names)')
param additionalAppSettings object = {}

@description('Resource tags')
param tags object = {}

var baseAppSettings = {
  AzureWebJobsStorage: '@Microsoft.KeyVault(SecretUri=${storageConnectionStringSecretUri})'
  WEBSITE_CONTENTAZUREFILECONNECTIONSTRING: '@Microsoft.KeyVault(SecretUri=${storageConnectionStringSecretUri})'
  WEBSITE_CONTENTSHARE: toLower(functionAppName)
  FUNCTIONS_EXTENSION_VERSION: '~4'
  FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
  APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
  'Cosmos:Endpoint': cosmosEndpoint
  'Cosmos:DatabaseName': cosmosDatabaseName
  'Cosmos:Key': '@Microsoft.KeyVault(SecretUri=${cosmosPrimaryKeySecretUri})'
  'ServiceBus:ConnectionString': '@Microsoft.KeyVault(SecretUri=${serviceBusConnectionStringSecretUri})'
}

var mergedAppSettings = union(baseAppSettings, additionalAppSettings)
var functionAppTags = union(tags, { service: serviceName })

// ------------------------------------------------------------------
// Hosting Plan - Consumption (Y1), Windows
// Fix: Linux Consumption plans are backed by a VM scale set and are
// gated by a regional VM-family quota (some subscriptions default to
// 0 for this until a quota increase is requested - see
// https://aka.ms/arm-deployment-operations). Windows Consumption is
// fully multi-tenant and does not hit this gate, while remaining in
// the same free tier (1M executions/month regardless of OS).
// ------------------------------------------------------------------
resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: hostingPlanName
  location: location
  tags: functionAppTags
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  kind: 'functionapp'
  properties: {
    reserved: false // Windows
  }
}

// ------------------------------------------------------------------
// Function App - .NET 8 Isolated, Windows Consumption
// ------------------------------------------------------------------
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  tags: functionAppTags
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [for key in items(mergedAppSettings): {
        name: key.key
        value: key.value
      }]
    }
  }
}

output functionAppId string = functionApp.id
output functionAppName string = functionApp.name
output functionAppDefaultHostName string = functionApp.properties.defaultHostName
output functionAppPrincipalId string = functionApp.identity.principalId

@description('Default ("default") host key for this Function App - used as the APIM backend credential.')
@secure()
output defaultFunctionKey string = listKeys('${functionApp.id}/host/default', '2023-12-01').functionKeys.default
