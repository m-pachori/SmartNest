// ============================================================
//  SmartNest - Cosmos DB Module
//  Serverless capacity mode, one database, one container per
//  bounded context.
//
//  Fix H3: all API versions now use stable GA releases.
//  Fix M3: enableFreeTier is now a param (default false);
//           pass true only in dev.parameters.json.
//  Fix C1: cosmosPrimaryKey is @secure() and NOT output as
//           plain text - caller (main.bicep) passes it to the
//           Key Vault module.
// ============================================================
@description('Azure region for the Cosmos DB account')
param location string

@description('Name of the Cosmos DB account')
param accountName string

@description('Logical database name')
param databaseName string = 'smartnest-db'

@description('Enable Cosmos DB free tier. Only valid for the first Cosmos account in a subscription. Set true for dev only.')
param enableFreeTier bool = false

@description('Resource tags')
param tags object = {}

// ------------------------------------------------------------------
// Cosmos DB Account - Serverless capacity mode
// Fix H3: stable GA API version 2023-11-15 (already stable; kept)
// ------------------------------------------------------------------
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-11-15' = {
  name: accountName
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
    // Serverless - no idle RU/s charge; billed per actual RU consumed
    capabilities: [
      { name: 'EnableServerless' }
    ]
    // M3: enableFreeTier is now parameterised - only dev should set this true.
    // Free tier is silently ignored if another Cosmos account already claims it
    // in the same subscription (staging/prod default to false).
    enableFreeTier: enableFreeTier
    backupPolicy: {
      type: 'Periodic'
      periodicModeProperties: {
        backupIntervalInMinutes: 240
        backupRetentionIntervalInHours: 8
        backupStorageRedundancy: 'Local'
      }
    }
    networkAclBypass: 'AzureServices'
    publicNetworkAccess: 'Enabled'
    enableAnalyticalStorage: false
    disableKeyBasedMetadataWriteAccess: false
  }
}

// ------------------------------------------------------------------
// Logical Database
// ------------------------------------------------------------------
resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-11-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

// ------------------------------------------------------------------
// Containers - one per bounded context
// Partition key strategy follows the dominant query pattern for each
// aggregate:
//   homes          → /homeId      (all home queries filter by homeId)
//   devices        → /homeId      (devices listed per home)
//   users          → /homeId      (users scoped to a home)
//   rules          → /homeId      (automation rules per home)
//   alerts         → /homeId      (alerts scoped to home)
//   audit-log      → /aggregateId (event replay queries by aggregateId)
//   summaries      → /homeId      (daily summaries per home)
//   media-metadata → /homeId      (media files per home)
// ------------------------------------------------------------------
var containers = [
  { name: 'homes',          partitionKey: '/homeId'      }
  { name: 'devices',        partitionKey: '/homeId'      }
  { name: 'users',          partitionKey: '/homeId'      }
  { name: 'rules',          partitionKey: '/homeId'      }
  { name: 'alerts',         partitionKey: '/homeId'      }
  { name: 'audit-log',      partitionKey: '/aggregateId' }
  { name: 'summaries',      partitionKey: '/homeId'      }
  { name: 'media-metadata', partitionKey: '/homeId'      }
]

resource cosmosContainers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = [
  for container in containers: {
    parent: cosmosDatabase
    name: container.name
    properties: {
      resource: {
        id: container.name
        partitionKey: {
          paths: [ container.partitionKey ]
          kind: 'Hash'
          version: 2
        }
        // TTL: -1 means no automatic expiry; adjust per service as needed
        defaultTtl: -1
        indexingPolicy: {
          indexingMode: 'consistent'
          automatic: true
          includedPaths: [ { path: '/*' } ]
          excludedPaths: [ { path: '/"_etag"/?' } ]
        }
      }
    }
  }
]

// ------------------------------------------------------------------
// Outputs
// Fix C1: cosmosPrimaryKey is marked @secure() - the value is passed
//          to the Key Vault module by main.bicep and is NOT stored in
//          deployment history as a plain string.
// ------------------------------------------------------------------
output cosmosAccountId string = cosmosAccount.id
output cosmosAccountName string = cosmosAccount.name
output cosmosEndpoint string = cosmosAccount.properties.documentEndpoint
output cosmosDatabaseName string = cosmosDatabase.name

@description('Primary key - passed directly to the Key Vault module. Never log or store elsewhere.')
@secure()
output cosmosPrimaryKey string = cosmosAccount.listKeys().primaryMasterKey
