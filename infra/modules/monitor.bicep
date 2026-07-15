// ============================================================
//  SmartNest - Azure Monitor Module
//  Dashboard + Alert rule on App Insights exception rate.
// ============================================================
@description('Azure region')
param location string

@description('Application Insights resource ID')
param appInsightsId string

@description('Application Insights name (used for metric queries)')
param appInsightsName string

@description('Log Analytics workspace resource ID - used for the log ingestion volume alert (Usage table)')
param logAnalyticsWorkspaceId string

@description('Email address for alert notifications')
param alertEmailAddress string

@description('Resource tags')
param tags object = {}

// ------------------------------------------------------------------
// Action Group - email notification target
// ------------------------------------------------------------------
resource alertActionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'smartnest-alerts-ag'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'SN-Alerts'
    enabled: true
    emailReceivers: [
      {
        name: 'SmartNestOpsEmail'
        emailAddress: alertEmailAddress
        useCommonAlertSchema: true
      }
    ]
  }
}

// ------------------------------------------------------------------
// Alert rule - App Insights exception rate threshold
// Fires when exceptions/5min > 5 (adjust threshold as needed)
// ------------------------------------------------------------------
resource exceptionRateAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'smartnest-high-exception-rate'
  location: 'global'
  tags: tags
  properties: {
    description: 'Fires when the Azure Functions exception count exceeds threshold in a 5-minute window'
    severity: 2       // Warning
    enabled: true
    scopes: [ appInsightsId ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'ExceptionCountCriteria'
          criterionType: 'StaticThresholdCriterion'
          metricNamespace: 'microsoft.insights/components'
          metricName: 'exceptions/count'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Count'
          skipMetricValidation: false
        }
      ]
    }
    actions: [
      {
        actionGroupId: alertActionGroup.id
        webHookProperties: {}
      }
    ]
    autoMitigate: true
    targetResourceType: 'microsoft.insights/components'
    targetResourceRegion: location
  }
}

// ------------------------------------------------------------------
// Alert rule - App Insights ingestion approaching 5 GB/month limit
// Azure Monitor uses daily byte volume on the Log Analytics workspace
// ------------------------------------------------------------------
// ------------------------------------------------------------------
// Alert rule - App Insights ingestion approaching 5 GB/month limit
// Implemented as a log query alert (scheduledQueryRules) against the
// Log Analytics workspace's `Usage` table - there is no metric named
// 'billedsize' on microsoft.insights/components, so a metric alert
// cannot express this; the Usage table is the supported source for
// ingested-data-volume alerting.
// ------------------------------------------------------------------
resource ingestionAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'smartnest-log-ingestion-warning'
  location: location
  tags: tags
  properties: {
    displayName: 'SmartNest - Daily Log Ingestion Warning'
    description: 'Fires when daily log ingestion exceeds 150 MB (approaching free tier 5 GB/month limit)'
    severity: 3       // Informational
    enabled: true
    scopes: [ logAnalyticsWorkspaceId ]
    evaluationFrequency: 'PT1H'
    windowSize: 'P1D'
    criteria: {
      allOf: [
        {
          query: 'Usage | where IsBillable == true | summarize IngestedMB = sum(Quantity) by bin(TimeGenerated, 1d)'
          timeAggregation: 'Maximum'
          metricMeasureColumn: 'IngestedMB'
          operator: 'GreaterThan'
          threshold: 150
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [ alertActionGroup.id ]
    }
    autoMitigate: true
  }
}

// ------------------------------------------------------------------
// Azure Monitor Dashboard
// ------------------------------------------------------------------
resource monitorDashboard 'Microsoft.Portal/dashboards@2020-09-01-preview' = {
  name: 'smartnest-dashboard'
  location: location
  tags: union(tags, { 'hidden-title': 'SmartNest Operations Dashboard' })
  properties: {
    lenses: [
      {
        order: 0
        parts: [
          // Widget 1 - Device event throughput
          {
            position: { x: 0, y: 0, colSpan: 6, rowSpan: 4 }
            metadata: {
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              inputs: [
                {
                  name: 'options'
                  value: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: { id: appInsightsId }
                          name: 'customMetrics/device.state.changes'
                          aggregationType: 1    // Average
                          namespace: 'microsoft.insights/components/kusto'
                          metricVisualization: { displayName: 'Device State Changes/min' }
                        }
                      ]
                      title: 'Device Event Throughput'
                      titleKind: 1
                      visualization: { chartType: 2 }    // Line chart
                      timespan: { relative: { duration: 86400000 } }
                    }
                  }
                }
              ]
            }
          }
          // Widget 2 - Exception rate
          {
            position: { x: 6, y: 0, colSpan: 6, rowSpan: 4 }
            metadata: {
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              inputs: [
                {
                  name: 'options'
                  value: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: { id: appInsightsId }
                          name: 'exceptions/count'
                          aggregationType: 1
                          namespace: 'microsoft.insights/components'
                          metricVisualization: { displayName: 'Exception Count' }
                        }
                      ]
                      title: 'Exception Rate'
                      titleKind: 1
                      visualization: { chartType: 2 }
                      timespan: { relative: { duration: 86400000 } }
                    }
                  }
                }
              ]
            }
          }
          // Widget 3 - Summary job status (custom metric)
          {
            position: { x: 0, y: 4, colSpan: 6, rowSpan: 4 }
            metadata: {
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              inputs: [
                {
                  name: 'options'
                  value: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: { id: appInsightsId }
                          name: 'customMetrics/automation.rules.triggered'
                          aggregationType: 1
                          namespace: 'microsoft.insights/components/kusto'
                          metricVisualization: { displayName: 'Automation Rules Triggered' }
                        }
                      ]
                      title: 'Automation Rules Triggered'
                      titleKind: 1
                      visualization: { chartType: 2 }
                      timespan: { relative: { duration: 86400000 } }
                    }
                  }
                }
              ]
            }
          }
          // Widget 4 - Media processing lag (alerts dispatched)
          {
            position: { x: 6, y: 4, colSpan: 6, rowSpan: 4 }
            metadata: {
              type: 'Extension/HubsExtension/PartType/MonitorChartPart'
              inputs: [
                {
                  name: 'options'
                  value: {
                    chart: {
                      metrics: [
                        {
                          resourceMetadata: { id: appInsightsId }
                          name: 'customMetrics/alerts.dispatched'
                          aggregationType: 1
                          namespace: 'microsoft.insights/components/kusto'
                          metricVisualization: { displayName: 'Alerts Dispatched' }
                        }
                      ]
                      title: 'Alerts Dispatched'
                      titleKind: 1
                      visualization: { chartType: 2 }
                      timespan: { relative: { duration: 86400000 } }
                    }
                  }
                }
              ]
            }
          }
        ]
      }
    ]
    metadata: {
      model: {
        timeRange: {
          value: {
            relative: { duration: 24, timeUnit: 1 }
          }
          type: 'MsPortalFx.Composition.Configuration.ValueTypes.TimeRange'
        }
      }
    }
  }
}

// ------------------------------------------------------------------
// Outputs
// ------------------------------------------------------------------
output actionGroupId string = alertActionGroup.id
output dashboardId string = monitorDashboard.id
