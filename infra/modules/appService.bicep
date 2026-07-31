@description('Azure region for the App Service')
param location string

@description('Name of the App Service (must be globally unique — becomes <name>.azurewebsites.net)')
param name string

@description('Resource ID of the App Service Plan')
param appServicePlanId string

@description('Resource ID of the user-assigned managed identity')
param identityId string

@description('Client ID of the user-assigned managed identity (tells DefaultAzureCredential which identity to use)')
param identityClientId string

param cosmosEndpoint string
param cosmosDatabaseName string
param storageBlobEndpoint string
param keyVaultUri string
param appInsightsConnectionString string

@description('Google OAuth client ID used to validate ID tokens. Deliberately NOT @secure() — it ships inside the frontend JS bundle and is public by design.')
param googleClientId string = ''

@description('Google OAuth client secret. Used only by the Google Drive connect flow — sign-in is an ID-token flow and never sees it.')
@secure()
param googleClientSecret string = ''

@description('Where Google returns the browser after the Drive consent screen. Must be on the public front door, not this App Service hostname.')
param driveRedirectUri string = ''

@description('The accounts seeded on first startup, which bootstrap the sign-in allowlist. Further users are added in-app via the Users page rather than here.')
@secure()
param seedUser1 {
  email: string
  displayName: string
  tempPassword: string
}

@secure()
param seedUser2 {
  email: string
  displayName: string
  tempPassword: string
}

resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: false // not supported on the F1 (free) plan tier
      ftpsState: 'Disabled'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'AZURE_CLIENT_ID', value: identityClientId }
        { name: 'Cosmos__AccountEndpoint', value: cosmosEndpoint }
        { name: 'Cosmos__DatabaseName', value: cosmosDatabaseName }
        { name: 'Storage__AccountUrl', value: storageBlobEndpoint }
        { name: 'KeyVault__Uri', value: keyVaultUri }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        { name: 'Authentication__Google__ClientId', value: googleClientId }
        { name: 'Authentication__Google__ClientSecret', value: googleClientSecret }
        { name: 'Authentication__Google__DriveRedirectUri', value: driveRedirectUri }
        { name: 'Seed__Users__0__Email', value: seedUser1.email }
        { name: 'Seed__Users__0__DisplayName', value: seedUser1.displayName }
        { name: 'Seed__Users__0__TempPassword', value: seedUser1.tempPassword }
        { name: 'Seed__Users__1__Email', value: seedUser2.email }
        { name: 'Seed__Users__1__DisplayName', value: seedUser2.displayName }
        { name: 'Seed__Users__1__TempPassword', value: seedUser2.tempPassword }
      ]
    }
  }
}

output name string = appService.name
output defaultHostName string = appService.properties.defaultHostName
output id string = appService.id
