// ============================================================
//  SmartNest — Service Bus Module
//  Standard tier (required for topics).
//  Topics:  device-events, home-events, user-events
//  Queue:   media-processing (replaced by blob trigger per plan,
//           retained for optional future use)
// ============================================================
@description('Azure region')
param location string

@description('Service Bus namespace name')
param namespaceName string

@description('Resource tags')
param tags object = {}

// ------------------------------------------------------------------
// Service Bus Namespace — Standard tier (topics require Standard+)
// ------------------------------------------------------------------
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
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
// Topics
// ------------------------------------------------------------------
var topicsConfig = [
  {
    name: 'device-events'
    // device-events is high-frequency; retain messages for 1 day
    defaultMessageTimeToLive: 'P1D'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    enablePartitioning: false
    subscriptions: [
      { name: 'automation', lockDuration: 'PT1M', maxDeliveryCount: 5 }
      { name: 'alert',      lockDuration: 'PT1M', maxDeliveryCount: 5 }
      { name: 'audit',      lockDuration: 'PT2M', maxDeliveryCount: 10 }
    ]
  }
  {
    name: 'home-events'
    defaultMessageTimeToLive: 'P3D'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    enablePartitioning: false
    subscriptions: [
      { name: 'audit', lockDuration: 'PT2M', maxDeliveryCount: 10 }
    ]
  }
  {
    name: 'user-events'
    defaultMessageTimeToLive: 'P3D'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    enablePartitioning: false
    subscriptions: [
      { name: 'audit', lockDuration: 'PT2M', maxDeliveryCount: 10 }
    ]
  }
]

resource topics 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = [
  for topic in topicsConfig: {
    parent: serviceBusNamespace
    name: topic.name
    properties: {
      defaultMessageTimeToLive: topic.defaultMessageTimeToLive
      maxSizeInMegabytes: topic.maxSizeInMegabytes
      requiresDuplicateDetection: topic.requiresDuplicateDetection
      enablePartitioning: topic.enablePartitioning
      enableBatchedOperations: true
      supportOrdering: false
      status: 'Active'
    }
  }
]

// Subscriptions must be created after their parent topic.
// Bicep does not support nested loops directly, so each topic's
// subscriptions are declared inline below.

// --- device-events subscriptions ---
resource deviceEventsAutomationSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: topics[0]
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

resource deviceEventsAlertSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: topics[0]
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

resource deviceEventsAuditSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: topics[0]
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
resource homeEventsAuditSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: topics[1]
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
resource userEventsAuditSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: topics[2]
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
// Queues
// ------------------------------------------------------------------
resource mediaProcessingQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'media-processing'
  properties: {
    defaultMessageTimeToLive: 'P1D'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    requiresSession: false
    deadLetteringOnMessageExpiration: true
    lockDuration: 'PT2M'
    maxDeliveryCount: 5
    enableBatchedOperations: true
    enablePartitioning: false
    status: 'Active'
  }
}

// ------------------------------------------------------------------
// Shared access policies (listen + send per service)
// ------------------------------------------------------------------
resource deviceServiceSendRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'DeviceServiceSend'
  properties: {
    rights: [ 'Send' ]
  }
}

resource auditServiceListenRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'AuditServiceListen'
  properties: {
    rights: [ 'Listen' ]
  }
}

resource functionsRootRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'FunctionsRoot'
  properties: {
    rights: [ 'Listen', 'Send', 'Manage' ]
  }
}

// ------------------------------------------------------------------
// Outputs
// ------------------------------------------------------------------
output serviceBusNamespaceId string = serviceBusNamespace.id
output serviceBusNamespaceName string = serviceBusNamespace.name
output serviceBusEndpoint string = serviceBusNamespace.properties.serviceBusEndpoint
output functionsConnectionString string = functionsRootRule.listKeys().primaryConnectionString
output deviceServiceSendConnectionString string = deviceServiceSendRule.listKeys().primaryConnectionString
output auditServiceListenConnectionString string = auditServiceListenRule.listKeys().primaryConnectionString
