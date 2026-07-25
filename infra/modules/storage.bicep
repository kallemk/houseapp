@description('Azure region for the storage account')
param location string

@description('Globally-unique storage account name (lowercase, no dashes, <=24 chars)')
param name string

@description('Principal ID of the managed identity that needs blob data-plane access')
param principalId string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    cors: {
      corsRules: [
        {
          // Uploads/downloads go straight from the browser to Blob Storage via short-lived SAS
          // URLs, which is a genuinely cross-origin call (unlike the API, which is same-origin via
          // the Static Web App's linked backend). Security here comes from the SAS signature and
          // its short expiry, not from origin restriction, so '*' is an acceptable simplification
          // for a 2-user app rather than wiring the Static Web App's hostname back into this module.
          allowedOrigins: ['*']
          allowedMethods: ['GET', 'PUT', 'HEAD', 'OPTIONS']
          allowedHeaders: ['*']
          exposedHeaders: ['*']
          maxAgeInSeconds: 3600
        }
      ]
    }
  }
}

resource documentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'documents'
  properties: {
    publicAccess: 'None'
  }
}

resource dataProtectionKeysContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'dataprotection-keys'
  properties: {
    publicAccess: 'None'
  }
}

// "Storage Blob Data Contributor" built-in role — read/write/delete blob data, no account key access.
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource blobDataContributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, principalId, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
  }
}

output name string = storageAccount.name
output blobEndpoint string = storageAccount.properties.primaryEndpoints.blob
