@description('Azure region for Application Insights')
param location string

@description('Name of the Application Insights resource')
param name string

@description('Resource ID of the Log Analytics workspace backing this workspace-based instance')
param workspaceId string

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspaceId
    IngestionMode: 'LogAnalytics'
  }
}

output connectionString string = appInsights.properties.ConnectionString
