// ============================================================
//  SmartNest - Key Vault Module
//  Stores all deployment secrets so they are never exposed as
//  plain-text Bicep outputs or in deployment history.
//
//  Secrets written here:
//    cosmos-primary-key
//    storage-connection-string
//    servicebus-functions-connection-string
//    servicebus-devicesvc-send-connection-string
//    servicebus-auditsvc-listen-connection-string
//
//  Access model:
//    - APIM managed identity gets Get/List on secrets
//    - Function Apps should use managed identity references
//      to read secrets at runtime (no connection strings in
//      app settings plain text)
// ============================================================
@description('Azure region')
param location string

@description('Key Vault name (3–24 alphanumeric and hyphens, globally unique)')
param keyVaultName string

@description('Object ID of the APIM system-assigned managed identity - granted Get/List')
param apimPrincipalId string

@description('Resource tags')
param tags object = {}

// ------------------------------------------------------------------
// Cosmos DB primary key - passed in from cosmos-db module
// ------------------------------------------------------------------
@description('Cosmos DB primary master key')
@secure()
param cosmosPrimaryKey string

// ------------------------------------------------------------------
// Storage connection string - passed in from storage module
// ------------------------------------------------------------------
@description('Storage account connection string')
@secure()
param storageConnectionString string

// ------------------------------------------------------------------
// Service Bus connection strings - passed in from service-bus module
// ------------------------------------------------------------------
@description('Service Bus Functions root connection string')
@secure()
param serviceBusFunctionsConnectionString string

@description('Service Bus DeviceService send-only connection string')
@secure()
param serviceBusDeviceSvcSendConnectionString string

@description('Service Bus AuditService listen-only connection string')
@secure()
param serviceBusAuditSvcListenConnectionString string

// ------------------------------------------------------------------
// Key Vault - Standard tier, soft-delete enabled, purge protection on
// ------------------------------------------------------------------
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: true
    enableRbacAuthorization: true          // use RBAC not access policies
    publicNetworkAccess: 'Enabled'         // restrict further with Private Endpoint when ready
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// ------------------------------------------------------------------
// RBAC: grant APIM managed identity Key Vault Secrets User
// (built-in role ID: 4633458b-17de-408a-b874-0445c86b69e6)
// ------------------------------------------------------------------
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource apimKvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, apimPrincipalId, kvSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: apimPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ------------------------------------------------------------------
// Secrets
// ------------------------------------------------------------------
resource secretCosmosPrimaryKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'cosmos-primary-key'
  properties: {
    value: cosmosPrimaryKey
    attributes: { enabled: true }
  }
}

resource secretStorageConnectionString 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'storage-connection-string'
  properties: {
    value: storageConnectionString
    attributes: { enabled: true }
  }
}

resource secretSbFunctions 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'servicebus-functions-connection-string'
  properties: {
    value: serviceBusFunctionsConnectionString
    attributes: { enabled: true }
  }
}

resource secretSbDeviceSvc 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'servicebus-devicesvc-send-connection-string'
  properties: {
    value: serviceBusDeviceSvcSendConnectionString
    attributes: { enabled: true }
  }
}

resource secretSbAuditSvc 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'servicebus-auditsvc-listen-connection-string'
  properties: {
    value: serviceBusAuditSvcListenConnectionString
    attributes: { enabled: true }
  }
}

// ------------------------------------------------------------------
// Outputs - URIs only, never secret values
// ------------------------------------------------------------------
output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri

output cosmosPrimaryKeySecretUri string = secretCosmosPrimaryKey.properties.secretUri
output storageConnectionStringSecretUri string = secretStorageConnectionString.properties.secretUri
output serviceBusFunctionsSecretUri string = secretSbFunctions.properties.secretUri
output serviceBusDeviceSvcSendSecretUri string = secretSbDeviceSvc.properties.secretUri
output serviceBusAuditSvcListenSecretUri string = secretSbAuditSvc.properties.secretUri
