@description('Azure region for the Cosmos DB account')
param location string

@description('Globally-unique Cosmos DB account name')
param name string

@description('Principal ID of the managed identity that needs data-plane access')
param principalId string

@description('Name of the SQL (NoSQL) database to create')
param databaseName string = 'houseapp'

// Serverless (pay-per-request, no idle floor, no cold start) rather than provisioned throughput —
// the cheapest and simplest fit for two people using the app occasionally.
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-08-15' = {
  name: name
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    // No account keys are ever issued — the API authenticates purely via Entra ID (see the role
    // assignment below), so there is no connection secret to store or rotate.
    disableLocalAuth: true
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-08-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

// renovationEntries/renovationTypes are superseded by projects/propertyComponents but deliberately
// kept: the migration copies rather than moves, so they are the rollback path if the project model
// misbehaves in production. Removing them is a separate decision, once the new model has settled.
var containerDefinitions = [
  { name: 'users', partitionKeyPath: '/id' }
  { name: 'properties', partitionKeyPath: '/id' }
  { name: 'valuationEntries', partitionKeyPath: '/propertyId' }
  { name: 'renovationEntries', partitionKeyPath: '/propertyId' }
  { name: 'documents', partitionKeyPath: '/propertyId' }
  { name: 'renovationTypes', partitionKeyPath: '/id' }
  { name: 'projects', partitionKeyPath: '/propertyId' }
  { name: 'propertyComponents', partitionKeyPath: '/id' }
  { name: 'budgets', partitionKeyPath: '/propertyId' }
]

// No container for the maintenance schedule on purpose: it is derived from propertyComponents'
// recommended intervals plus the newest completed maintenance project per component, so storing it
// would only create a copy that drifts. See MaintenanceScheduleController.

resource containers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-08-15' = [
  for c in containerDefinitions: {
    parent: database
    name: c.name
    properties: {
      resource: {
        id: c.name
        partitionKey: {
          paths: [c.partitionKeyPath]
          kind: 'Hash'
        }
      }
    }
  }
]

// Built-in "Cosmos DB Built-in Data Contributor" role definition — grants read/write data-plane
// access via Entra ID. This is a data-plane role assignment (sqlRoleAssignments), distinct from an
// Azure RBAC (control-plane) role assignment.
var dataContributorRoleDefinitionId = '00000000-0000-0000-0000-000000000002'

resource dataRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-08-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, principalId, dataContributorRoleDefinitionId)
  properties: {
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/${dataContributorRoleDefinitionId}'
    principalId: principalId
    scope: cosmosAccount.id
  }
}

output endpoint string = cosmosAccount.properties.documentEndpoint
output accountName string = cosmosAccount.name
