@description('Name of the budget')
param name string

@description('Monthly amount, in the subscription\'s billing currency')
param amount int

@description('Where the alerts go. Azure requires at least one address on a resource-group budget — roles alone are not enough.')
param contactEmail string

@description('First day of a month. Fixed rather than derived from utcNow(): a moving date would try to change the budget on every deploy, and Azure refuses to change a budget\'s start date.')
param startDate string

// A cost *alarm*, not a cap — Azure has no way to stop serverless Cosmos or Blob Storage at a
// spending limit. What it does is send an email the same day, rather than the
// overspend surfacing on the next invoice. Scoped to this resource group, which holds exactly the
// usage-priced resources (Cosmos, Storage); the App Service plan lives in rg-common and is a fixed
// monthly cost either way.
resource budget 'Microsoft.Consumption/budgets@2023-05-01' = {
  name: name
  properties: {
    category: 'Cost'
    amount: amount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: startDate
    }
    // The address plus the Owner role, so whoever owns the subscription or this resource group hears
    // about it too.
    notifications: {
      actual80: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [ contactEmail ]
        contactRoles: [ 'Owner' ]
      }
      actual100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: [ contactEmail ]
        contactRoles: [ 'Owner' ]
      }
      forecast100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [ contactEmail ]
        contactRoles: [ 'Owner' ]
      }
    }
  }
}
