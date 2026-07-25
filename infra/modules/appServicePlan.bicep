@description('Azure region for the App Service Plan')
param location string

@description('Name of the App Service Plan')
param name string

@description('SKU name — F1 (free) by default; bump to B1 (~$13/mo, always-on, no daily quota) if F1 quota becomes limiting')
param sku string = 'F1'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: name
  location: location
  kind: 'linux'
  sku: {
    name: sku
  }
  properties: {
    reserved: true
  }
}

output id string = plan.id
