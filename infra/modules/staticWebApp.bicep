@description('Azure region for the Static Web App — must be one of the limited set of regions Static Web Apps supports (e.g. westeurope, westus2, centralus, eastus2, eastasia)')
param location string

@description('Name of the Static Web App')
param name string

@description('Resource ID of the App Service to register as the linked (same-origin, /api/*) backend')
param linkedBackendId string

@description('Azure region of the linked backend App Service')
param linkedBackendLocation string

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: name
  location: location
  // Linked backends (the /api/* proxy the cookie-auth design relies on) are a Standard-tier-only
  // feature — Free does not support them, and deployment fails with "SkuCode 'Free' is invalid"
  // on the linkedBackends resource below if this is set to Free.
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    // No repositoryUrl/branch here — content is deployed directly by the frontend-ci-cd GitHub
    // Actions workflow via the deployment token (see main.bicep's output), not GitHub integration.
    provider: 'None'
  }
}

// Proxies /api/* through the Static Web App's own origin, which is what lets cookie auth use
// SameSite=Lax with no CORS configuration — see the cookie auth setup in ServiceCollectionExtensions.cs.
resource linkedBackend 'Microsoft.Web/staticSites/linkedBackends@2023-12-01' = {
  parent: staticWebApp
  name: 'default'
  properties: {
    backendResourceId: linkedBackendId
    region: linkedBackendLocation
  }
}

output hostname string = staticWebApp.properties.defaultHostname
output id string = staticWebApp.id
