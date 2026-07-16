// ============================================================
//  SmartNest - Service Bus Module
//  Standard tier (required for topics).
//  Topics:  device-events, home-events, user-events
//  Queue:   media-processing removed - Task 9 (Media Service) uses a
//           Blob trigger as its sole processing path instead.
//
//  Fix H3: all API versions changed from 2022-10-01-preview to
//           stable GA 2021-11-01.
//  Fix C1: all connection string outputs are @secure() and are
//           NOT returned as plain strings - caller (main.bicep)
//           passes them directly to the Key Vault module.
//  Fix L2: subscriptions now reference their parent topic via
//           a named resource variable, not a fragile array index.
// ============================================================
@description('Azure region')
param location string

@description('Service Bus namespace name')
param namespaceName string

@description('Resource tags')
param tags object = {}

// ------------------------------------------------------------------
// Service Bus Namespace - Standard tier (topics require Standard+)
// Fix H3: stable GA API version 2021-11-01
// ------------------------------------------------------------------
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2021-11-01' = {
  name: namespaceName
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
  }
}

// ------------------------------------------------------------------
// Topics - declared as named resources (Fix L2: no array index coupling)
// ------------------------------------------------------------------
resource deviceEventsTopic 'Microsoft.ServiceBus/namespaces/topics@2021-11-01' = {
  parent: serviceBusNamespace
  name: 'device-events'
  properties: {
    // device-events is high-frequency; retain messages for 1 day
    defaultMessageTimeToLive: 'P1D'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    enablePartitioning: false
    enableBatchedOperations: true
    supportOrdering: false
    status: 'Active'
  }
}

resource homeEventsTopic 'Microsoft.ServiceBus/namespaces/topics@2021-11-01' = {
  parent: serviceBusNamespace
  name: 'home-events'
  properties: {
    defaultMessageTimeToLive: 'P3D'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    enablePartitioning: false
    enableBatchedOperations: true
    supportOrdering: false
    status: 'Active'
  }
}

resource userEventsTopic 'Microsoft.ServiceBus/namespaces/topics@2021-11-01' = {
  parent: serviceBusNamespace
  name: 'user-events'
  properties: {
    defaultMessageTimeToLive: 'P3D'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    enablePartitioning: false
    enableBatchedOperations: true
    supportOrdering: false
    status: 'Active'
  }
}

// ------------------------------------------------------------------
// Subscriptions - each references its named parent topic (Fix L2)
// ------------------------------------------------------------------

// --- device-events subscriptions ---
resource deviceEventsAutomationSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2021-11-01' = {
  parent: deviceEventsTopic
  name: 'automation'
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 5
    deadLetteringOnMessageExpiration: true
    requiresSession: false
    enableBatchedOperations: true
    status: 'Active'
  }
}

resource deviceEventsAlertSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2021-11-01' = {
  parent: deviceEventsTopic
  name: 'alert'
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 5
    deadLetteringOnMessageExpiration: true
    requiresSession: false
    enableBatchedOperations: true
    status: 'Active'
  }
}

resource deviceEventsAuditSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2021-11-01' = {
  parent: deviceEventsTopic
  name: 'audit'
  properties: {
    lockDuration: 'PT2M'
    maxDeliveryCount: 10
    deadLetteringOnMessageExpiration: true
    requiresSession: false
    enableBatchedOperations: true
    status: 'Active'
  }
}

// --- home-events subscriptions ---
resource homeEventsAuditSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2021-11-01' = {
  parent: homeEventsTopic
  name: 'audit'
  properties: {
    lockDuration: 'PT2M'
    maxDeliveryCount: 10
    deadLetteringOnMessageExpiration: true
    requiresSession: false
    enableBatchedOperations: true
    status: 'Active'
  }
}

// --- user-events subscriptions ---
resource userEventsAuditSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2021-11-01' = {
  parent: userEventsTopic
  name: 'audit'
  properties: {
    lockDuration: 'PT2M'
    maxDeliveryCount: 10
    deadLetteringOnMessageExpiration: true
    requiresSession: false
    enableBatchedOperations: true
    status: 'Active'
  }
}

// ------------------------------------------------------------------
// Shared access policies (least-privilege per service)
// ------------------------------------------------------------------
resource deviceServiceSendRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2021-11-01' = {
  parent: serviceBusNamespace
  name: 'DeviceServiceSend'
  properties: {
    rights: [ 'Send' ]
  }
}

resource identityServiceSendRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2021-11-01' = {
  parent: serviceBusNamespace
  name: 'IdentityServiceSend'
  properties: {
    rights: [ 'Send' ]
  }
}

// PlatformServiceSendListen: used by the merged Automation/Alert/Audit/
// Summary/Media Function App (Tasks 5-9 - see plan-platformService.prompt.md).
// Needs both rights since it consumes device-events/home-events/user-events
// subscriptions (Automation/Alert/Audit) AND publishes AutomationExecuted/
// AlertRaised/SummaryGenerated/DocumentProcessed - unlike Device/Identity
// Service, which only ever publish.
resource platformServiceSendListenRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2021-11-01' = {
  parent: serviceBusNamespace
  name: 'PlatformServiceSendListen'
  properties: {
    rights: [ 'Listen', 'Send' ]
  }
}

resource functionsRootRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2021-11-01' = {
  parent: serviceBusNamespace
  name: 'FunctionsRoot'
  properties: {
    rights: [ 'Listen', 'Send', 'Manage' ]
  }
}

// ------------------------------------------------------------------
// Outputs
// Fix C1: all connection strings are @secure() - passed to Key Vault
//          by main.bicep, never stored as plain deployment outputs.
// ------------------------------------------------------------------
output serviceBusNamespaceId string = serviceBusNamespace.id
output serviceBusNamespaceName string = serviceBusNamespace.name
output serviceBusEndpoint string = serviceBusNamespace.properties.serviceBusEndpoint

@description('Functions root connection string - passed directly to Key Vault. Never log.')
@secure()
output functionsConnectionString string = functionsRootRule.listKeys().primaryConnectionString

@description('DeviceService send-only connection string - passed directly to Key Vault. Never log.')
@secure()
output deviceServiceSendConnectionString string = deviceServiceSendRule.listKeys().primaryConnectionString

@description('IdentityService send-only connection string - passed directly to Key Vault. Never log.')
@secure()
output identityServiceSendConnectionString string = identityServiceSendRule.listKeys().primaryConnectionString

@description('PlatformService (Tasks 5-9) Listen+Send connection string - passed directly to Key Vault. Never log.')
@secure()
output platformServiceSendListenConnectionString string = platformServiceSendListenRule.listKeys().primaryConnectionString
