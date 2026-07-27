targetScope = 'resourceGroup'

@description('Base name used to derive resource names')
param environmentName string = 'houseapp'

@description('Azure region for most resources')
param location string = resourceGroup().location

@description('Azure region for the Static Web App — must be one of the limited set of supported regions')
param staticWebAppLocation string = 'westeurope'

@description('App Service Plan SKU — F1 is free; bump to B1 (~$13/mo, always-on) if the F1 daily quota becomes limiting')
param appServiceSku string = 'F1'

@description('Google OAuth client ID for Sign in with Google. Public by design (it ships in the frontend bundle) so it is not @secure() and can live in main.parameters.json; this flow uses no client secret.')
param googleClientId string = ''

@description('First bootstrap account — seeded on first startup so someone can sign in and manage the rest via the in-app Users page')
@secure()
param seedUser1 {
  email: string
  displayName: string
  tempPassword: string
}

@description('Second bootstrap account — seeded on first startup so someone can sign in and manage the rest via the in-app Users page')
@secure()
param seedUser2 {
  email: string
  displayName: string
  tempPassword: string
}

var uniqueSuffix = uniqueString(resourceGroup().id)
var cosmosDatabaseName = 'houseapp'

var names = {
  identity: '${environmentName}-identity'
  keyVault: take('${environmentName}kv${uniqueSuffix}', 24)
  cosmos: '${environmentName}-cosmos-${uniqueSuffix}'
  storage: take(toLower('${environmentName}st${uniqueSuffix}'), 24)
  logAnalytics: '${environmentName}-logs'
  appInsights: '${environmentName}-insights'
  appServicePlan: '${environmentName}-plan'
  appService: '${environmentName}-api-${uniqueSuffix}'
  staticWebApp: '${environmentName}-web'
}

module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: {
    location: location
    name: names.identity
  }
}

module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    name: names.keyVault
    principalId: identity.outputs.principalId
  }
}

module cosmos 'modules/cosmos.bicep' = {
  name: 'cosmos'
  params: {
    location: location
    name: names.cosmos
    principalId: identity.outputs.principalId
    databaseName: cosmosDatabaseName
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    name: names.storage
    principalId: identity.outputs.principalId
  }
}

module logAnalytics 'modules/logAnalytics.bicep' = {
  name: 'logAnalytics'
  params: {
    location: location
    name: names.logAnalytics
  }
}

module appInsights 'modules/appInsights.bicep' = {
  name: 'appInsights'
  params: {
    location: location
    name: names.appInsights
    workspaceId: logAnalytics.outputs.id
  }
}

module appServicePlan 'modules/appServicePlan.bicep' = {
  name: 'appServicePlan'
  params: {
    location: location
    name: names.appServicePlan
    sku: appServiceSku
  }
}

module appService 'modules/appService.bicep' = {
  name: 'appService'
  params: {
    location: location
    name: names.appService
    appServicePlanId: appServicePlan.outputs.id
    identityId: identity.outputs.id
    identityClientId: identity.outputs.clientId
    cosmosEndpoint: cosmos.outputs.endpoint
    cosmosDatabaseName: cosmosDatabaseName
    storageBlobEndpoint: storage.outputs.blobEndpoint
    keyVaultUri: keyVault.outputs.uri
    appInsightsConnectionString: appInsights.outputs.connectionString
    googleClientId: googleClientId
    seedUser1: seedUser1
    seedUser2: seedUser2
  }
}

module staticWebApp 'modules/staticWebApp.bicep' = {
  name: 'staticWebApp'
  params: {
    location: staticWebAppLocation
    name: names.staticWebApp
    linkedBackendId: appService.outputs.id
    linkedBackendLocation: location
  }
}

output appServiceName string = appService.outputs.name
output appServiceHostname string = appService.outputs.defaultHostName
output staticWebAppHostname string = staticWebApp.outputs.hostname
output cosmosEndpoint string = cosmos.outputs.endpoint
output storageAccountName string = storage.outputs.name
output keyVaultUri string = keyVault.outputs.uri
