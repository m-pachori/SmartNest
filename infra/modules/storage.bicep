// ============================================================
//  SmartNest - Storage Module
//  Azure Blob Storage with media-uploads, processed-media, and
//  snapshots containers.
//
//  Fix C1: storageConnectionString is @secure() and is NOT
//           returned as a plain output - caller (main.bicep)
//           passes it directly to the Key Vault module.
// ============================================================
@description('Azure region')
param location string

@description('Storage account name (3-24 lowercase alphanumeric)')
param storageAccountName string

@description('Resource tags')
param tags object = {}

// ------------------------------------------------------------------
// Storage Account - LRS, Standard (Free Tier: 5 GB LRS)
// ------------------------------------------------------------------
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false         // no public anonymous access
    allowSharedKeyAccess: true           // Functions use connection string
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
    encryption: {
      services: {
        blob: { enabled: true }
        file: { enabled: true }
      }
      keySource: 'Microsoft.Storage'
    }
  }
}

// ------------------------------------------------------------------
// Blob Service settings
// ------------------------------------------------------------------
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 7          // soft-delete for 7 days - safety net during POC
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
    cors: {
      corsRules: []    // no browser clients; APIM is the entry point
    }
  }
}

// ------------------------------------------------------------------
// Blob Containers
// ------------------------------------------------------------------
resource mediaUploadsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'media-uploads'
  properties: {
    publicAccess: 'None'
  }
}

resource processedMediaContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'processed-media'
  properties: {
    publicAccess: 'None'
  }
}

// Snapshots container - used by Event Sourcing / Audit Service
resource snapshotsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'snapshots'
  properties: {
    publicAccess: 'None'
  }
}

// ------------------------------------------------------------------
// Outputs
// Fix C1: storageConnectionString is @secure() - passed to the Key
//          Vault module by main.bicep, never stored in plain text.
// ------------------------------------------------------------------
output storageAccountId string = storageAccount.id
output storageAccountName string = storageAccount.name
output primaryBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob

@description('Connection string - passed directly to Key Vault module. Never log or store elsewhere.')
@secure()
output storageConnectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
