// ============================================================
//  SmartNest - Main Bicep Orchestration
//  Deploys all Azure resources for the SmartNest platform.
//
//  Target scope: resourceGroup
//  (Resource Group must already exist - created via CLI /
//  pipeline before this deployment runs.)
//
//  Changes from initial version:
//    Fix C1: Key Vault module wired in; all secrets are written
//             to Key Vault at deploy time and never surfaced as
//             plain Bicep outputs.
//    Fix M1: appInsightsResourceId passed to APIM module.
//    Fix M3: enableFreeTier passed to cosmos module (true only
//             for dev via parameters file).
//
//  Usage:
//    az deployment group create \
//      --resource-group smartnest-rg-dev \
//      --template-file infra/main.bicep \
//      --parameters @infra/parameters/dev.parameters.json \
//      --parameters @/tmp/param-overrides.json
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

@description('Project name - used as a prefix in resource names')
param projectName string = 'smartnest'

@description('Cosmos DB account name')
param cosmosAccountName string = '${projectName}-cosmos-${environment}'

@description('Cosmos DB logical database name')
param cosmosDatabaseName string = 'smartnest-db'

@description('Enable Cosmos DB free tier. Valid only for the first Cosmos account in a subscription. Set true for dev, false for staging/prod.')
param cosmosEnableFreeTier bool = false

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

@description('Azure AD Tenant ID - injected into JWT validation policy')
param tenantId string

@description('Azure AD App Registration Client ID for smartnest-api')
param apiAppClientId string

@description('Email address for Azure Monitor alert notifications')
param monitorAlertEmailAddress string

@description('Key Vault name (3–24 alphanumeric and hyphens, globally unique)')
param keyVaultName string = '${projectName}-kv-${environment}'

// ------------------------------------------------------------------
// Shared Tags - applied to every resource
// ------------------------------------------------------------------
var commonTags = {
  project: projectName
  environment: environment
  managedBy: 'bicep'
  costCenter: 'free-tier'
}

// ------------------------------------------------------------------
// Module: Application Insights + Log Analytics
// (deployed first - other modules consume the connection string)
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
    enableFreeTier: cosmosEnableFreeTier    // Fix M3: controlled per environment
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
// Fix M1: appInsightsResourceId now passed (was appInsightsConnectionString)
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
    appInsightsResourceId: appInsightsModule.outputs.appInsightsId      // Fix M1
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
// Module: Key Vault - stores all secrets; never expose plain values
// Fix C1: all secrets from cosmos/storage/servicebus modules are
//          written here. Outputs below return Key Vault secret URIs
//          only, not the raw secret values.
// ------------------------------------------------------------------
module keyVaultModule 'modules/key-vault.bicep' = {
  name: 'deploy-key-vault'
  params: {
    location: location
    keyVaultName: keyVaultName
    apimPrincipalId: apimModule.outputs.apimManagedIdentityPrincipalId
    cosmosPrimaryKey: cosmosModule.outputs.cosmosPrimaryKey
    storageConnectionString: storageModule.outputs.storageConnectionString
    serviceBusFunctionsConnectionString: serviceBusModule.outputs.functionsConnectionString
    serviceBusDeviceSvcSendConnectionString: serviceBusModule.outputs.deviceServiceSendConnectionString
    serviceBusAuditSvcListenConnectionString: serviceBusModule.outputs.auditServiceListenConnectionString
    tags: commonTags
  }
  dependsOn: [ apimModule, cosmosModule, storageModule, serviceBusModule ]
}

// ------------------------------------------------------------------
// Outputs - consumed by CI/CD pipelines and developer scripts
// Fix C1: no secret values are output here. Use Key Vault secret URIs
//          to wire Function App settings via Key Vault references.
// ------------------------------------------------------------------

// App Insights
output appInsightsConnectionString string = appInsightsModule.outputs.connectionString
output appInsightsInstrumentationKey string = appInsightsModule.outputs.instrumentationKey
output logAnalyticsWorkspaceId string = appInsightsModule.outputs.logAnalyticsWorkspaceId

// Cosmos DB (non-secret)
output cosmosEndpoint string = cosmosModule.outputs.cosmosEndpoint
output cosmosAccountName string = cosmosModule.outputs.cosmosAccountName

// Service Bus (non-secret)
output serviceBusEndpoint string = serviceBusModule.outputs.serviceBusEndpoint
output serviceBusNamespaceName string = serviceBusModule.outputs.serviceBusNamespaceName

// Storage (non-secret)
output storagePrimaryBlobEndpoint string = storageModule.outputs.primaryBlobEndpoint
output storageAccountName string = storageModule.outputs.storageAccountName

// APIM
output apimGatewayUrl string = apimModule.outputs.apimGatewayUrl
output apimManagedIdentityPrincipalId string = apimModule.outputs.apimManagedIdentityPrincipalId

// Key Vault - reference URIs for wiring into Function App settings
output keyVaultUri string = keyVaultModule.outputs.keyVaultUri
output cosmosPrimaryKeySecretUri string = keyVaultModule.outputs.cosmosPrimaryKeySecretUri
output storageConnectionStringSecretUri string = keyVaultModule.outputs.storageConnectionStringSecretUri
output serviceBusFunctionsSecretUri string = keyVaultModule.outputs.serviceBusFunctionsSecretUri
output serviceBusDeviceSvcSendSecretUri string = keyVaultModule.outputs.serviceBusDeviceSvcSendSecretUri
output serviceBusAuditSvcListenSecretUri string = keyVaultModule.outputs.serviceBusAuditSvcListenSecretUri
