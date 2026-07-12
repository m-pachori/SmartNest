// ============================================================
//  SmartNest — Main Bicep Orchestration
//  Deploys all Azure resources for the SmartNest platform.
//
//  Target scope: resourceGroup
//  (Resource Group must already exist — created via CLI /
//  setup scripts before this deployment runs.)
//
//  Usage:
//    az deployment group create \
//      --resource-group smartnest-rg \
//      --template-file infra/main.bicep \
//      --parameters @infra/parameters/dev.parameters.json
// ============================================================
targetScope = 'resourceGroup'

// ------------------------------------------------------------------
// Parameters
// ------------------------------------------------------------------
@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Short environment label (dev | staging | prod)')
@allowed([ 'dev', 'staging', 'prod' ])
param environment string = 'dev'

@description('Project name — used as a prefix in resource names')
param projectName string = 'smartnest'

@description('Cosmos DB account name')
param cosmosAccountName string = '${projectName}-cosmos-${environment}'

@description('Cosmos DB logical database name')
param cosmosDatabaseName string = 'smartnest-db'

@description('Service Bus namespace name')
param serviceBusNamespaceName string = '${projectName}-bus-${environment}'

@description('Storage account name (max 24 chars, lowercase alphanumeric)')
@maxLength(24)
param storageAccountName string = '${projectName}storage${environment}'

@description('Application Insights resource name')
param appInsightsName string = '${projectName}-insights-${environment}'

@description('Log Analytics workspace name')
param logAnalyticsWorkspaceName string = '${projectName}-logs-${environment}'

@description('API Management instance name')
param apimName string = '${projectName}-apim-${environment}'

@description('APIM publisher email')
param apimPublisherEmail string

@description('APIM publisher display name')
param apimPublisherName string = 'SmartNest Admin'

@description('Azure AD Tenant ID — injected into JWT validation policy')
param tenantId string

@description('Azure AD App Registration Client ID for smartnest-api')
param apiAppClientId string

@description('Email address for Azure Monitor alert notifications')
param monitorAlertEmailAddress string

// ------------------------------------------------------------------
// Shared Tags — applied to every resource
// ------------------------------------------------------------------
var commonTags = {
  project: projectName
  environment: environment
  managedBy: 'bicep'
  costCenter: 'free-tier'
}

// ------------------------------------------------------------------
// Module: Application Insights + Log Analytics
// (deployed first — other modules consume the connection string)
// ------------------------------------------------------------------
module appInsightsModule 'modules/app-insights.bicep' = {
  name: 'deploy-app-insights'
  params: {
    location: location
    appInsightsName: appInsightsName
    logAnalyticsWorkspaceName: logAnalyticsWorkspaceName
    tags: commonTags
  }
}

// ------------------------------------------------------------------
// Module: Cosmos DB (Serverless)
// ------------------------------------------------------------------
module cosmosModule 'modules/cosmos-db.bicep' = {
  name: 'deploy-cosmos-db'
  params: {
    location: location
    accountName: cosmosAccountName
    databaseName: cosmosDatabaseName
    tags: commonTags
  }
}

// ------------------------------------------------------------------
// Module: Service Bus (Standard)
// ------------------------------------------------------------------
module serviceBusModule 'modules/service-bus.bicep' = {
  name: 'deploy-service-bus'
  params: {
    location: location
    namespaceName: serviceBusNamespaceName
    tags: commonTags
  }
}

// ------------------------------------------------------------------
// Module: Storage Account + Blob Containers
// ------------------------------------------------------------------
module storageModule 'modules/storage.bicep' = {
  name: 'deploy-storage'
  params: {
    location: location
    storageAccountName: storageAccountName
    tags: commonTags
  }
}

// ------------------------------------------------------------------
// Module: API Management (Developer tier)
// ------------------------------------------------------------------
module apimModule 'modules/apim.bicep' = {
  name: 'deploy-apim'
  params: {
    location: location
    apimName: apimName
    publisherEmail: apimPublisherEmail
    publisherName: apimPublisherName
    tenantId: tenantId
    apiClientId: apiAppClientId
    appInsightsConnectionString: appInsightsModule.outputs.connectionString
    appInsightsInstrumentationKey: appInsightsModule.outputs.instrumentationKey
    tags: commonTags
  }
  dependsOn: [ appInsightsModule ]
}

// ------------------------------------------------------------------
// Module: Azure Monitor Dashboard + Alert Rules
// ------------------------------------------------------------------
module monitorModule 'modules/monitor.bicep' = {
  name: 'deploy-monitor'
  params: {
    location: location
    appInsightsId: appInsightsModule.outputs.appInsightsId
    appInsightsName: appInsightsModule.outputs.appInsightsName
    alertEmailAddress: monitorAlertEmailAddress
    tags: commonTags
  }
  dependsOn: [ appInsightsModule ]
}

// ------------------------------------------------------------------
// Outputs — consumed by CI/CD pipelines and developer scripts
// ------------------------------------------------------------------

// App Insights
output appInsightsConnectionString string = appInsightsModule.outputs.connectionString
output appInsightsInstrumentationKey string = appInsightsModule.outputs.instrumentationKey
output logAnalyticsWorkspaceId string = appInsightsModule.outputs.logAnalyticsWorkspaceId

// Cosmos DB
output cosmosEndpoint string = cosmosModule.outputs.cosmosEndpoint
output cosmosAccountName string = cosmosModule.outputs.cosmosAccountName
@description('Primary key — store in Key Vault; do not log')
output cosmosPrimaryKey string = cosmosModule.outputs.cosmosPrimaryKey

// Service Bus
output serviceBusEndpoint string = serviceBusModule.outputs.serviceBusEndpoint
output serviceBusNamespaceName string = serviceBusModule.outputs.serviceBusNamespaceName
@description('Functions root connection string — store in Key Vault; do not log')
output serviceBusFunctionsConnectionString string = serviceBusModule.outputs.functionsConnectionString

// Storage
output storagePrimaryBlobEndpoint string = storageModule.outputs.primaryBlobEndpoint
output storageAccountName string = storageModule.outputs.storageAccountName
@description('Storage connection string — store in Key Vault; do not log')
output storageConnectionString string = storageModule.outputs.storageConnectionString

// APIM
output apimGatewayUrl string = apimModule.outputs.apimGatewayUrl
output apimManagedIdentityPrincipalId string = apimModule.outputs.apimManagedIdentityPrincipalId
