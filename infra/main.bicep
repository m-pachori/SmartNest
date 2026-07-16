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

@description('Whether to deploy API Management and register service APIs with it. Disabled by default - APIM is currently not used (calling Function Apps directly / via function keys for now); the apim.bicep/apim-api.bicep modules and JWT policy are kept as-is for later re-enablement, just not deployed while this is false.')
param deployApim bool = false

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

@description('Home Service Function App name')
param homeServiceFunctionAppName string = '${projectName}-home-svc-${environment}'

@description('Home Service Function App hosting plan name')
param homeServiceHostingPlanName string = '${projectName}-home-svc-plan-${environment}'

@description('Device Service Function App name')
param deviceServiceFunctionAppName string = '${projectName}-device-svc-${environment}'

@description('Device Service Function App hosting plan name')
param deviceServiceHostingPlanName string = '${projectName}-device-svc-plan-${environment}'

@description('Identity Service Function App name')
param identityServiceFunctionAppName string = '${projectName}-identity-svc-${environment}'

@description('Identity Service Function App hosting plan name')
param identityServiceHostingPlanName string = '${projectName}-identity-svc-plan-${environment}'

@description('Platform Service Function App name - single merged Function App hosting Automation, Alert, Audit, Summary, and Media (Tasks 5-9) instead of five separate apps/plans - see plan-platformService.prompt.md.')
param platformServiceFunctionAppName string = '${projectName}-platform-svc-${environment}'

@description('Platform Service Function App hosting plan name')
param platformServiceHostingPlanName string = '${projectName}-platform-svc-plan-${environment}'

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
// Disabled by default (tech debt) - see deployApim param above. The
// module itself is left untouched so it can be re-enabled later by
// simply setting deployApim to true.
// ------------------------------------------------------------------
module apimModule 'modules/apim.bicep' = if (deployApim) {
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
    logAnalyticsWorkspaceId: appInsightsModule.outputs.logAnalyticsWorkspaceId
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
    apimPrincipalId: deployApim ? apimModule.outputs.apimManagedIdentityPrincipalId : ''
    cosmosPrimaryKey: cosmosModule.outputs.cosmosPrimaryKey
    storageConnectionString: storageModule.outputs.storageConnectionString
    serviceBusFunctionsConnectionString: serviceBusModule.outputs.functionsConnectionString
    serviceBusDeviceSvcSendConnectionString: serviceBusModule.outputs.deviceServiceSendConnectionString
    serviceBusIdentitySvcSendConnectionString: serviceBusModule.outputs.identityServiceSendConnectionString
    serviceBusPlatformSvcSendListenConnectionString: serviceBusModule.outputs.platformServiceSendListenConnectionString
    tags: commonTags
  }
  dependsOn: [ apimModule, cosmosModule, storageModule, serviceBusModule ]
}

// ------------------------------------------------------------------
// Module: Home Service Function App (Task 2)
// Consumption plan, .NET 8 Isolated. Reuses the shared platform
// storage account, Cosmos DB, Service Bus, and App Insights.
// ------------------------------------------------------------------
module homeFunctionAppModule 'modules/function-app.bicep' = {
  name: 'deploy-home-function-app'
  params: {
    location: location
    serviceName: 'home'
    functionAppName: homeServiceFunctionAppName
    hostingPlanName: homeServiceHostingPlanName
    storageConnectionString: storageModule.outputs.storageConnectionString
    cosmosEndpoint: cosmosModule.outputs.cosmosEndpoint
    cosmosDatabaseName: cosmosDatabaseName
    cosmosPrimaryKeySecretUri: keyVaultModule.outputs.cosmosPrimaryKeySecretUri
    serviceBusConnectionStringSecretUri: keyVaultModule.outputs.serviceBusFunctionsSecretUri
    appInsightsConnectionString: appInsightsModule.outputs.connectionString
    additionalAppSettings: {
      'Cosmos:HomesContainerName': 'homes'
      'Cosmos:DevicesContainerName': 'devices'
      'Cosmos:UsersContainerName': 'users'
    }
    tags: commonTags
  }
}

// ------------------------------------------------------------------
// Grant the Home Service Function App's managed identity access to
// read secrets from Key Vault (required — Key Vault uses RBAC, not
// access policies; see infra/modules/key-vault.bicep).
// ------------------------------------------------------------------
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource existingKeyVaultForRoleAssignment 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
  dependsOn: [ keyVaultModule ]
}

resource homeFunctionAppKvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(existingKeyVaultForRoleAssignment.id, homeServiceFunctionAppName, kvSecretsUserRoleId)
  scope: existingKeyVaultForRoleAssignment
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: homeFunctionAppModule.outputs.functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ------------------------------------------------------------------
// Store the Home Service Function App's default host key in Key
// Vault so APIM can reference it as a named value (see apim-api.bicep).
// Only needed when APIM is deployed (tech debt - disabled by default).
// ------------------------------------------------------------------
resource homeSvcFunctionKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (deployApim) {
  parent: existingKeyVaultForRoleAssignment
  name: 'home-svc-function-key'
  properties: {
    value: homeFunctionAppModule.outputs.defaultFunctionKey
  }
}

// ------------------------------------------------------------------
// Module: Register the Home Service API in APIM (route prefix /homes)
// Disabled by default (tech debt) - see deployApim param above.
// ------------------------------------------------------------------
module homeApiModule 'modules/apim-api.bicep' = if (deployApim) {
  name: 'deploy-home-api'
  params: {
    apimServiceName: apimModule.outputs.apimServiceName
    apiName: 'homes'
    apiDisplayName: 'Home Service'
    backendHostName: homeFunctionAppModule.outputs.functionAppDefaultHostName
    functionKeySecretUri: homeSvcFunctionKeySecret.properties.secretUri
    productName: 'smartnest-backend'
    operations: [
      { name: 'create-home', displayName: 'Create Home', method: 'POST', urlTemplate: '/homes' }
      {
        name: 'get-home'
        displayName: 'Get Home'
        method: 'GET'
        urlTemplate: '/homes/{id}'
        templateParameters: [ { name: 'id', type: 'string', required: true } ]
      }
      {
        name: 'update-home'
        displayName: 'Update Home'
        method: 'PUT'
        urlTemplate: '/homes/{id}'
        templateParameters: [ { name: 'id', type: 'string', required: true } ]
      }
      {
        name: 'delete-home'
        displayName: 'Delete Home'
        method: 'DELETE'
        urlTemplate: '/homes/{id}'
        templateParameters: [ { name: 'id', type: 'string', required: true } ]
      }
      {
        name: 'add-room'
        displayName: 'Add Room'
        method: 'POST'
        urlTemplate: '/homes/{id}/rooms'
        templateParameters: [ { name: 'id', type: 'string', required: true } ]
      }
      {
        name: 'remove-room'
        displayName: 'Remove Room'
        method: 'DELETE'
        urlTemplate: '/homes/{id}/rooms/{roomId}'
        templateParameters: [
          { name: 'id', type: 'string', required: true }
          { name: 'roomId', type: 'string', required: true }
        ]
      }
    ]
  }
}

// ------------------------------------------------------------------
// Module: Device Service Function App (Task 3)
// Consumption plan, .NET 8 Isolated. Reuses the shared platform
// storage account, Cosmos DB, App Insights, and the least-privilege
// DeviceServiceSend (send-only) Service Bus connection string -
// Device Service only ever publishes to device-events, never listens.
// ------------------------------------------------------------------
module deviceFunctionAppModule 'modules/function-app.bicep' = {
  name: 'deploy-device-function-app'
  params: {
    location: location
    serviceName: 'device'
    functionAppName: deviceServiceFunctionAppName
    hostingPlanName: deviceServiceHostingPlanName
    storageConnectionString: storageModule.outputs.storageConnectionString
    cosmosEndpoint: cosmosModule.outputs.cosmosEndpoint
    cosmosDatabaseName: cosmosDatabaseName
    cosmosPrimaryKeySecretUri: keyVaultModule.outputs.cosmosPrimaryKeySecretUri
    serviceBusConnectionStringSecretUri: keyVaultModule.outputs.serviceBusDeviceSvcSendSecretUri
    appInsightsConnectionString: appInsightsModule.outputs.connectionString
    additionalAppSettings: {
      'Cosmos:DevicesContainerName': 'devices'
      // Read-only lookup used for the ownership check (Cosmos-level home
      // ownership verification, mirroring Home Service - see
      // IHomeOwnershipRepository) instead of trusting the JWT homeId claim.
      'Cosmos:HomesContainerName': 'homes'
    }
    tags: commonTags
  }
}

// ------------------------------------------------------------------
// Grant the Device Service Function App's managed identity access to
// read secrets from Key Vault (required — Key Vault uses RBAC, not
// access policies; see infra/modules/key-vault.bicep).
// ------------------------------------------------------------------
resource deviceFunctionAppKvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(existingKeyVaultForRoleAssignment.id, deviceServiceFunctionAppName, kvSecretsUserRoleId)
  scope: existingKeyVaultForRoleAssignment
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: deviceFunctionAppModule.outputs.functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ------------------------------------------------------------------
// Store the Device Service Function App's default host key in Key
// Vault. Always created (not gated by deployApim) - APIM can reference
// it as a named value when enabled, and the Platform Service Function
// App (Tasks 5-9) always needs it for Automation's in-process calls to
// Device Service's PATCH /devices/{id}/state endpoint (see
// plan-platformService.prompt.md Decisions).
// ------------------------------------------------------------------
resource deviceSvcFunctionKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: existingKeyVaultForRoleAssignment
  name: 'device-svc-function-key'
  properties: {
    value: deviceFunctionAppModule.outputs.defaultFunctionKey
  }
}

// ------------------------------------------------------------------
// Module: Register the Device Service API in APIM (route prefix /devices,
// plus POST /homes/{homeId}/devices for registration)
// Disabled by default (tech debt) - see deployApim param above.
// ------------------------------------------------------------------
module deviceApiModule 'modules/apim-api.bicep' = if (deployApim) {
  name: 'deploy-device-api'
  params: {
    apimServiceName: apimModule.outputs.apimServiceName
    apiName: 'devices'
    apiDisplayName: 'Device Service'
    backendHostName: deviceFunctionAppModule.outputs.functionAppDefaultHostName
    functionKeySecretUri: deviceSvcFunctionKeySecret.properties.secretUri
    productName: 'smartnest-backend'
    operations: [
      {
        name: 'register-device'
        displayName: 'Register Device'
        method: 'POST'
        urlTemplate: '/homes/{homeId}/devices'
        templateParameters: [ { name: 'homeId', type: 'string', required: true } ]
      }
      {
        name: 'get-device'
        displayName: 'Get Device'
        method: 'GET'
        urlTemplate: '/devices/{id}'
        templateParameters: [ { name: 'id', type: 'string', required: true } ]
      }
      {
        name: 'update-device-state'
        displayName: 'Update Device State'
        method: 'PATCH'
        urlTemplate: '/devices/{id}/state'
        templateParameters: [ { name: 'id', type: 'string', required: true } ]
      }
      {
        name: 'remove-device'
        displayName: 'Remove Device'
        method: 'DELETE'
        urlTemplate: '/devices/{id}'
        templateParameters: [ { name: 'id', type: 'string', required: true } ]
      }
    ]
  }
}

// ------------------------------------------------------------------
// Module: Identity Service Function App (Task 4)
// Consumption plan, .NET 8 Isolated. Reuses the shared platform
// storage account, Cosmos DB, App Insights, and the least-privilege
// IdentityServiceSend (send-only) Service Bus connection string -
// Identity Service only ever publishes to user-events, never listens.
// ------------------------------------------------------------------
module identityFunctionAppModule 'modules/function-app.bicep' = {
  name: 'deploy-identity-function-app'
  params: {
    location: location
    serviceName: 'identity'
    functionAppName: identityServiceFunctionAppName
    hostingPlanName: identityServiceHostingPlanName
    storageConnectionString: storageModule.outputs.storageConnectionString
    cosmosEndpoint: cosmosModule.outputs.cosmosEndpoint
    cosmosDatabaseName: cosmosDatabaseName
    cosmosPrimaryKeySecretUri: keyVaultModule.outputs.cosmosPrimaryKeySecretUri
    serviceBusConnectionStringSecretUri: keyVaultModule.outputs.serviceBusIdentitySvcSendSecretUri
    appInsightsConnectionString: appInsightsModule.outputs.connectionString
    additionalAppSettings: {
      'Cosmos:UsersContainerName': 'users'
      // Read-only lookup used for the ownership check (Cosmos-level home
      // ownership verification, mirroring Home/Device Service - see
      // IHomeOwnershipRepository) instead of trusting the JWT homeId claim.
      'Cosmos:HomesContainerName': 'homes'
    }
    tags: commonTags
  }
}

// ------------------------------------------------------------------
// Grant the Identity Service Function App's managed identity access to
// read secrets from Key Vault (required — Key Vault uses RBAC, not
// access policies; see infra/modules/key-vault.bicep).
// ------------------------------------------------------------------
resource identityFunctionAppKvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(existingKeyVaultForRoleAssignment.id, identityServiceFunctionAppName, kvSecretsUserRoleId)
  scope: existingKeyVaultForRoleAssignment
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: identityFunctionAppModule.outputs.functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ------------------------------------------------------------------
// Store the Identity Service Function App's default host key in Key
// Vault so APIM can reference it as a named value (see apim-api.bicep).
// Only needed when APIM is deployed (tech debt - disabled by default).
// ------------------------------------------------------------------
resource identitySvcFunctionKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (deployApim) {
  parent: existingKeyVaultForRoleAssignment
  name: 'identity-svc-function-key'
  properties: {
    value: identityFunctionAppModule.outputs.defaultFunctionKey
  }
}

// ------------------------------------------------------------------
// Module: Register the Identity Service API in APIM (route prefix /users,
// plus POST /homes/{homeId}/users/invite for inviting members)
// Disabled by default (tech debt) - see deployApim param above.
// ------------------------------------------------------------------
module identityApiModule 'modules/apim-api.bicep' = if (deployApim) {
  name: 'deploy-identity-api'
  params: {
    apimServiceName: apimModule.outputs.apimServiceName
    apiName: 'users'
    apiDisplayName: 'Identity Service'
    backendHostName: identityFunctionAppModule.outputs.functionAppDefaultHostName
    functionKeySecretUri: identitySvcFunctionKeySecret.properties.secretUri
    productName: 'smartnest-backend'
    operations: [
      {
        name: 'invite-user'
        displayName: 'Invite User'
        method: 'POST'
        urlTemplate: '/homes/{homeId}/users/invite'
        templateParameters: [ { name: 'homeId', type: 'string', required: true } ]
      }
      {
        name: 'update-user-role'
        displayName: 'Update User Role'
        method: 'PUT'
        urlTemplate: '/users/{id}/role'
        templateParameters: [ { name: 'id', type: 'string', required: true } ]
      }
      {
        name: 'remove-user'
        displayName: 'Remove User'
        method: 'DELETE'
        urlTemplate: '/homes/{homeId}/users/{userId}'
        templateParameters: [
          { name: 'homeId', type: 'string', required: true }
          { name: 'userId', type: 'string', required: true }
        ]
      }
    ]
  }
}

// ------------------------------------------------------------------
// Module: Platform Service Function App (Tasks 5-9)
// Single merged Function App + hosting plan running Automation, Alert,
// Audit (event store), Summary, and Media instead of five separate
// Function Apps/plans - see plan-platformService.prompt.md. Reuses the
// shared platform storage account, Cosmos DB (rules/alerts/audit-log/
// summaries/media-metadata containers, already provisioned in Task 1),
// App Insights, and the combined Listen+Send PlatformServiceSendListen
// Service Bus connection string (this app both consumes the automation/
// alert/audit subscriptions and publishes AutomationExecuted/AlertRaised/
// SummaryGenerated/DocumentProcessed).
// ------------------------------------------------------------------
module platformFunctionAppModule 'modules/function-app.bicep' = {
  name: 'deploy-platform-function-app'
  params: {
    location: location
    serviceName: 'platform'
    functionAppName: platformServiceFunctionAppName
    hostingPlanName: platformServiceHostingPlanName
    storageConnectionString: storageModule.outputs.storageConnectionString
    cosmosEndpoint: cosmosModule.outputs.cosmosEndpoint
    cosmosDatabaseName: cosmosDatabaseName
    cosmosPrimaryKeySecretUri: keyVaultModule.outputs.cosmosPrimaryKeySecretUri
    serviceBusConnectionStringSecretUri: keyVaultModule.outputs.serviceBusPlatformSvcSendListenSecretUri
    appInsightsConnectionString: appInsightsModule.outputs.connectionString
    additionalAppSettings: {
      'Cosmos:HomesContainerName': 'homes'
      'Cosmos:DevicesContainerName': 'devices'
      'Cosmos:RulesContainerName': 'rules'
      'Cosmos:AlertsContainerName': 'alerts'
      'Cosmos:AuditLogContainerName': 'audit-log'
      'Cosmos:SummariesContainerName': 'summaries'
      'Cosmos:MediaMetadataContainerName': 'media-metadata'
      'Storage:MediaUploadsContainerName': 'media-uploads'
      'Storage:ProcessedMediaContainerName': 'processed-media'
      // Automation's "ChangeDeviceState" rule action calls Device Service's
      // HTTP endpoint directly - internal service-to-service call, not APIM.
      'DeviceService:BaseUrl': 'https://${deviceFunctionAppModule.outputs.functionAppDefaultHostName}/api'
      'DeviceService:FunctionKey': '@Microsoft.KeyVault(SecretUri=${deviceSvcFunctionKeySecret.properties.secretUri})'
    }
    tags: commonTags
  }
}

// ------------------------------------------------------------------
// Grant the Platform Service Function App's managed identity access to
// read secrets from Key Vault (required — Key Vault uses RBAC, not
// access policies; see infra/modules/key-vault.bicep).
// ------------------------------------------------------------------
resource platformFunctionAppKvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(existingKeyVaultForRoleAssignment.id, platformServiceFunctionAppName, kvSecretsUserRoleId)
  scope: existingKeyVaultForRoleAssignment
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: platformFunctionAppModule.outputs.functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
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

// APIM (disabled by default - see deployApim param; empty strings when not deployed)
output apimGatewayUrl string = deployApim ? apimModule.outputs.apimGatewayUrl : ''
output apimManagedIdentityPrincipalId string = deployApim ? apimModule.outputs.apimManagedIdentityPrincipalId : ''

// Key Vault - reference URIs for wiring into Function App settings
output keyVaultUri string = keyVaultModule.outputs.keyVaultUri
output cosmosPrimaryKeySecretUri string = keyVaultModule.outputs.cosmosPrimaryKeySecretUri
output storageConnectionStringSecretUri string = keyVaultModule.outputs.storageConnectionStringSecretUri
output serviceBusFunctionsSecretUri string = keyVaultModule.outputs.serviceBusFunctionsSecretUri
output serviceBusDeviceSvcSendSecretUri string = keyVaultModule.outputs.serviceBusDeviceSvcSendSecretUri
output serviceBusIdentitySvcSendSecretUri string = keyVaultModule.outputs.serviceBusIdentitySvcSendSecretUri
output serviceBusPlatformSvcSendListenSecretUri string = keyVaultModule.outputs.serviceBusPlatformSvcSendListenSecretUri

// Home Service (Task 2)
output homeServiceFunctionAppName string = homeFunctionAppModule.outputs.functionAppName
output homeServiceFunctionAppDefaultHostName string = homeFunctionAppModule.outputs.functionAppDefaultHostName
output homeServiceApiName string = deployApim ? homeApiModule.outputs.apiName : ''

// Device Service (Task 3)
output deviceServiceFunctionAppName string = deviceFunctionAppModule.outputs.functionAppName
output deviceServiceFunctionAppDefaultHostName string = deviceFunctionAppModule.outputs.functionAppDefaultHostName
output deviceServiceApiName string = deployApim ? deviceApiModule.outputs.apiName : ''

// Identity Service (Task 4)
output identityServiceFunctionAppName string = identityFunctionAppModule.outputs.functionAppName
output identityServiceFunctionAppDefaultHostName string = identityFunctionAppModule.outputs.functionAppDefaultHostName
output identityServiceApiName string = deployApim ? identityApiModule.outputs.apiName : ''

// Platform Service (Tasks 5-9: Automation, Alert, Audit, Summary, Media)
output platformServiceFunctionAppName string = platformFunctionAppModule.outputs.functionAppName
output platformServiceFunctionAppDefaultHostName string = platformFunctionAppModule.outputs.functionAppDefaultHostName
