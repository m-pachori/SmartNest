// ============================================================
//  SmartNest — API Management Module
//  Developer tier — single gateway for all HTTP Functions.
//  JWT validation policy applied at the global/API level.
// ============================================================
@description('Azure region')
param location string

@description('APIM instance name')
param apimName string

@description('Publisher email (required by APIM)')
param publisherEmail string

@description('Publisher display name')
param publisherName string

@description('Azure AD Tenant ID — used in JWT validation policy')
param tenantId string

@description('Azure AD App Registration Client ID (smartnest-api)')
param apiClientId string

@description('Application Insights connection string for APIM logging')
param appInsightsConnectionString string

@description('Application Insights instrumentation key')
param appInsightsInstrumentationKey string

@description('Resource tags')
param tags object = {}

// ------------------------------------------------------------------
// API Management Service — Developer tier (no SLA, suitable for POC)
// ------------------------------------------------------------------
resource apimService 'Microsoft.ApiManagement/service@2023-05-01-preview' = {
  name: apimName
  location: location
  tags: tags
  sku: {
    name: 'Developer'
    capacity: 1
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName
    customProperties: {
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Ciphers.TripleDes168': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls10': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls11': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Ssl30': 'false'
    }
    virtualNetworkType: 'None'
  }
}

// ------------------------------------------------------------------
// Named values — injected into the JWT policy XML at runtime
// ------------------------------------------------------------------
resource namedValueTenantId 'Microsoft.ApiManagement/service/namedValues@2023-05-01-preview' = {
  parent: apimService
  name: 'tenantId'
  properties: {
    displayName: 'tenantId'
    value: tenantId
    secret: false
    tags: [ 'identity' ]
  }
}

resource namedValueApiClientId 'Microsoft.ApiManagement/service/namedValues@2023-05-01-preview' = {
  parent: apimService
  name: 'apiClientId'
  properties: {
    displayName: 'apiClientId'
    value: apiClientId
    secret: false
    tags: [ 'identity' ]
  }
}

// ------------------------------------------------------------------
// Application Insights Logger for APIM
// ------------------------------------------------------------------
resource apimLogger 'Microsoft.ApiManagement/service/loggers@2023-05-01-preview' = {
  parent: apimService
  name: 'smartnest-insights-logger'
  properties: {
    loggerType: 'applicationInsights'
    description: 'Application Insights logger for SmartNest APIM'
    resourceId: appInsightsConnectionString   // connection string used for identification
    credentials: {
      instrumentationKey: appInsightsInstrumentationKey
    }
    isBuffered: true
  }
}

// ------------------------------------------------------------------
// Diagnostic settings — link APIM to App Insights
// ------------------------------------------------------------------
resource apimDiagnostics 'Microsoft.ApiManagement/service/diagnostics@2023-05-01-preview' = {
  parent: apimService
  name: 'applicationinsights'
  properties: {
    alwaysLog: 'allErrors'
    httpCorrelationProtocol: 'W3C'
    verbosity: 'information'
    logClientIp: false
    loggerId: apimLogger.id
    sampling: {
      samplingType: 'fixed'
      percentage: 25          // 25 % sampling to protect 5 GB/month quota
    }
    frontend: {
      request:  { headers: [], body: { bytes: 0 } }
      response: { headers: [], body: { bytes: 0 } }
    }
    backend: {
      request:  { headers: [], body: { bytes: 0 } }
      response: { headers: [], body: { bytes: 0 } }
    }
  }
}

// ------------------------------------------------------------------
// Global inbound policy — JWT validation applied to every API
// ------------------------------------------------------------------
resource apimGlobalPolicy 'Microsoft.ApiManagement/service/policies@2023-05-01-preview' = {
  parent: apimService
  name: 'policy'
  properties: {
    format: 'xml'
    value: '''
<policies>
  <inbound>
    <base />
    <validate-jwt header-name="Authorization"
                  failed-validation-httpcode="401"
                  failed-validation-error-message="Unauthorized: invalid or missing JWT"
                  require-expiration-time="true"
                  require-scheme="Bearer"
                  require-signed-tokens="true">
      <openid-config url="https://login.microsoftonline.com/{{tenantId}}/v2.0/.well-known/openid-configuration" />
      <audiences>
        <audience>api://{{apiClientId}}</audience>
      </audiences>
      <issuers>
        <issuer>https://login.microsoftonline.com/{{tenantId}}/v2.0</issuer>
        <issuer>https://sts.windows.net/{{tenantId}}/</issuer>
      </issuers>
      <required-claims>
        <claim name="roles" match="any">
          <value>SmartNest.Owner</value>
          <value>SmartNest.Technician</value>
          <value>SmartNest.Guest</value>
        </claim>
      </required-claims>
    </validate-jwt>
    <set-header name="x-correlation-id" exists-action="skip">
      <value>@(Guid.NewGuid().ToString())</value>
    </set-header>
    <set-header name="x-ms-client-principal-id" exists-action="override">
      <value>@(context.Request.Headers.GetValueOrDefault("Authorization","").AsJwt()?.Subject ?? "")</value>
    </set-header>
  </inbound>
  <backend><base /></backend>
  <outbound><base /></outbound>
  <on-error><base /></on-error>
</policies>
'''
  }
  dependsOn: [ namedValueTenantId, namedValueApiClientId ]
}

// ------------------------------------------------------------------
// Products — group APIs logically
// ------------------------------------------------------------------
resource smartNestProduct 'Microsoft.ApiManagement/service/products@2023-05-01-preview' = {
  parent: apimService
  name: 'smartnest-backend'
  properties: {
    displayName: 'SmartNest Backend APIs'
    description: 'All SmartNest microservice HTTP endpoints'
    state: 'published'
    subscriptionRequired: false   // JWT is the auth mechanism, not APIM subscription keys
    approvalRequired: false
  }
}

// ------------------------------------------------------------------
// Outputs
// ------------------------------------------------------------------
output apimServiceId string = apimService.id
output apimServiceName string = apimService.name
output apimGatewayUrl string = apimService.properties.gatewayUrl
output apimManagedIdentityPrincipalId string = apimService.identity.principalId
