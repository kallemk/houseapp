targetScope = 'resourceGroup'

@description('Base name used to derive resource names')
param environmentName string = 'houseapp'

@description('Azure region for most resources')
param location string = resourceGroup().location

@description('Resource group holding the shared App Service Plan. Deliberately not this deployment\'s own resource group — the plan is shared with other apps, so it is not created or owned here.')
param appServicePlanResourceGroup string = 'rg-common'

@description('Name of the shared App Service Plan (B1, Linux, Always On capable). Referenced, never created: this deployment does not own its lifecycle.')
param appServicePlanName string = 'plan-common-001'

@description('Google OAuth client ID for Sign in with Google. Public by design (it ships in the frontend bundle) so it is not @secure() and can live in main.parameters.json.')
param googleClientId string = ''

@description('Google OAuth client secret — used ONLY by the Google Drive connect flow, never by sign-in (that is an ID-token flow with no secret). The app\'s first real secret: it comes from the GOOGLE_CLIENT_SECRET GitHub secret and must never be committed or turned into a plain repo variable the way googleClientId is.')
@secure()
param googleClientSecret string = ''

@description('GitHub personal access token used to file user suggestions as issues. Fine-grained, scoped to this repository only, Issues: read and write. Comes from the GH_ISSUES_TOKEN GitHub secret — NOT GITHUB_TOKEN, which Actions reserves and refuses to let you create.')
@secure()
param gitHubToken string = ''

@description('Owner and repository that user suggestions are filed into.')
param gitHubOwner string = 'kallemk'
param gitHubRepo string = 'houseapp'

@description('Public origin the app is reached on — the custom domain bound to the App Service. Used to build the Google Drive OAuth redirect URI, which Google sends the *browser* to, so it must be the hostname people actually use rather than the *.azurewebsites.net one. Domain binding and its certificate are done outside Bicep (the binding needs DNS to resolve first, and the managed certificate needs the binding to exist), hence the default here.')
param appBaseUrl string = 'https://housetracker.odenbulten.se'

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
  // No "-api" suffix any more: this App Service serves the SPA as well as the API, so naming it
  // after half its job was misleading. The rename is also what makes the move to the shared plan
  // safe — a new name means a *create* rather than a plan change on the existing site, which Azure
  // refuses across resource groups (error 59602), so the old app keeps serving until this one is
  // verified and can then be deleted.
  appService: '${environmentName}-${uniqueSuffix}'
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

// Referenced, not created. The plan lives in another resource group and is shared with other apps,
// so this deployment must never own or modify it — an `existing` reference is what keeps a
// what-if/deploy here from proposing changes to something it doesn't own.
resource sharedAppServicePlan 'Microsoft.Web/serverfarms@2023-12-01' existing = {
  name: appServicePlanName
  scope: resourceGroup(appServicePlanResourceGroup)
}

module appService 'modules/appService.bicep' = {
  name: 'appService'
  params: {
    // The plan's region, not this resource group's: an App Service must sit in the same region as
    // its plan, and the two are no longer guaranteed to match now the plan lives elsewhere.
    //
    // Migration trap, one-time: an existing App Service's location is **immutable**. If this plan is
    // in a different region from the App Service that already exists, the deployment fails rather
    // than moving it — that case needs the old app deleted and recreated, which changes the
    // *.azurewebsites.net hostname. Check before deploying:
    //   az appservice plan show -g rg-common -n plan-common-001 --query location
    location: sharedAppServicePlan.location
    name: names.appService
    appServicePlanId: sharedAppServicePlan.id
    identityId: identity.outputs.id
    identityClientId: identity.outputs.clientId
    cosmosEndpoint: cosmos.outputs.endpoint
    cosmosDatabaseName: cosmosDatabaseName
    storageBlobEndpoint: storage.outputs.blobEndpoint
    keyVaultUri: keyVault.outputs.uri
    appInsightsConnectionString: appInsights.outputs.connectionString
    googleClientId: googleClientId
    googleClientSecret: googleClientSecret
    driveRedirectUri: '${appBaseUrl}/api/drive/callback'
    gitHubToken: gitHubToken
    gitHubOwner: gitHubOwner
    gitHubRepo: gitHubRepo
    seedUser1: seedUser1
    seedUser2: seedUser2
  }
}

// The Static Web App is gone: the App Service now serves the SPA itself, so there is nothing left to
// proxy. Note that removing it from here does NOT delete the deployed resource — Azure deployments
// are incremental — so it has to be deleted explicitly once the domain has moved:
//   az staticwebapp delete -g <rg> -n houseapp-web

output appServiceName string = appService.outputs.name
output appServiceHostname string = appService.outputs.defaultHostName
output cosmosEndpoint string = cosmos.outputs.endpoint
output storageAccountName string = storage.outputs.name
output keyVaultUri string = keyVault.outputs.uri
