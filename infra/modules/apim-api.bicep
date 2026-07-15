// ============================================================
//  SmartNest - APIM API Registration Module (generic, reusable)
//  Registers a single Function App backend as an API + operations
//  in an existing APIM instance, injecting the backend's Function
//  key via a Key Vault-backed named value. Reusable by Tasks 3-9.
// ============================================================
@description('Existing APIM service name')
param apimServiceName string

@description('API path segment / identifier, e.g. "homes"')
param apiName string

@description('API display name')
param apiDisplayName string

@description('Backend Function App default hostname, e.g. smartnest-home-svc-dev.azurewebsites.net')
param backendHostName string

@description('Key Vault secret URI containing the backend Function App default host key')
@secure()
param functionKeySecretUri string

@description('Existing APIM product name to associate this API with')
param productName string

@description('Operations to register: [{ name, displayName, method, urlTemplate }]')
param operations array

resource apimService 'Microsoft.ApiManagement/service@2022-08-01' existing = {
  name: apimServiceName
}

resource product 'Microsoft.ApiManagement/service/products@2022-08-01' existing = {
  parent: apimService
  name: productName
}

var namedValueName = '${apiName}-function-key'

resource namedValueFunctionKey 'Microsoft.ApiManagement/service/namedValues@2022-08-01' = {
  parent: apimService
  name: namedValueName
  properties: {
    displayName: namedValueName
    secret: true
    keyVault: {
      secretIdentifier: functionKeySecretUri
    }
  }
}

resource api 'Microsoft.ApiManagement/service/apis@2022-08-01' = {
  parent: apimService
  name: apiName
  properties: {
    displayName: apiDisplayName
    // Fix: path must be empty, not apiName. Each operation's urlTemplate
    // already contains the full desired public path (e.g. "/homes",
    // "/homes/{id}") — if the API also had a "path" prefix equal to
    // apiName ("homes"), the public gateway URL would be double-prefixed
    // (e.g. /homes/homes) while the backend serviceUrl+urlTemplate
    // combination (which doesn't involve "path") stays correct either way.
    path: ''
    protocols: [
      'https'
    ]
    serviceUrl: 'https://${backendHostName}/api'
    subscriptionRequired: false
  }
}

resource apiOperations 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = [for op in operations: {
  parent: api
  name: op.name
  properties: {
    displayName: op.displayName
    method: op.method
    urlTemplate: op.urlTemplate
    templateParameters: op.?templateParameters ?? []
  }
}]

// Inbound policy: inject the Function key so APIM can call the backend on
// callers' behalf. JWT validation itself is applied globally (see apim.bicep).
// Note: triple-quoted Bicep strings do not support ${...} interpolation, so the
// named-value reference is substituted via replace() after the fact.
var policyXmlTemplate = '''
<policies>
  <inbound>
    <base />
    <set-header name="x-functions-key" exists-action="override">
      <value>{{__FUNCTION_KEY_NAMED_VALUE__}}</value>
    </set-header>
  </inbound>
  <backend><base /></backend>
  <outbound><base /></outbound>
  <on-error><base /></on-error>
</policies>
'''

resource apiPolicy 'Microsoft.ApiManagement/service/apis/policies@2022-08-01' = {
  parent: api
  name: 'policy'
  properties: {
    format: 'xml'
    value: replace(policyXmlTemplate, '__FUNCTION_KEY_NAMED_VALUE__', namedValueName)
  }
  dependsOn: [
    namedValueFunctionKey
  ]
}

resource productApiLink 'Microsoft.ApiManagement/service/products/apis@2022-08-01' = {
  parent: product
  name: api.name
}

output apiId string = api.id
output apiName string = api.name
