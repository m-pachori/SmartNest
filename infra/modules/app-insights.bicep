// ============================================================
//  SmartNest - Application Insights Module
//  Log Analytics Workspace + Application Insights workspace.
//  All Function Apps share one Application Insights instance.
// ============================================================
@description('Azure region')
param location string

@description('Application Insights resource name')
param appInsightsName string

@description('Log Analytics workspace name')
param logAnalyticsWorkspaceName string

@description('Resource tags')
param tags object = {}

// ------------------------------------------------------------------
// Log Analytics Workspace (required for workspace-based App Insights)
// ------------------------------------------------------------------
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'   // Pay-As-You-Go; first 5 GB/month free
    }
    retentionInDays: 30   // minimum; keep low to stay within free tier
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    workspaceCapping: {
      dailyQuotaGb: 0.2   // ~200 MB/day cap - protects against runaway ingestion
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ------------------------------------------------------------------
// Application Insights - workspace-based (modern mode)
// ------------------------------------------------------------------
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    Flow_Type: 'Bluefield'
    Request_Source: 'rest'
    RetentionInDays: 30
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    // Adaptive sampling - reduces volume when ingestion nears free quota
    SamplingPercentage: null   // null = adaptive (recommended)
    DisableIpMasking: false
  }
}

// ------------------------------------------------------------------
// Outputs
// ------------------------------------------------------------------
output appInsightsId string = appInsights.id
output appInsightsName string = appInsights.name
output instrumentationKey string = appInsights.properties.InstrumentationKey
output connectionString string = appInsights.properties.ConnectionString
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id
