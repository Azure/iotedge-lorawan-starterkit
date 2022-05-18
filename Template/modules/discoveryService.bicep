param location string = resourceGroup().location
param sku string = 'B1'
param iotHubHostName string
param discoveryZipUrl string
param iotHubName string
param appInsightName string
param roleNameGuid string = guid(resourceGroup().id, 'twincontributor')
param uniqueSolutionPrefix string

var webAppName = '${uniqueSolutionPrefix}discovery'
var hostingPlanName = '${webAppName}plan'
var iotHubTwinContributorRoleId = '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/494bdba2-168f-4f31-a0a1-191d2f7c028c'
var aspNetCoreUrls = 'http://0.0.0.0:80;https://0.0.0.0:443'
var webSitesApiVersion = '2021-03-01'

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightName
}

resource iotHub 'Microsoft.Devices/IotHubs@2021-07-02' existing = {
  name: iotHubName
}

resource hostingPlan 'Microsoft.Web/serverfarms@2020-12-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: sku
  }
}

resource webApp 'Microsoft.Web/sites@2021-03-01' = {
  name: webAppName
  location: location
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'IotHubHostName'
          value: iotHubHostName
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: discoveryZipUrl
        }
        {
          name: 'ASPNETCORE_URLS'
          value: aspNetCoreUrls
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: reference(appInsights.id, '2015-05-01').InstrumentationKey
        }
      ]
      webSocketsEnabled: true
    }
  }
}

// resource roleAssignment 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = {
//   name: roleNameGuid
//   scope: iotHub
//   properties: {
//     principalId: webApp.identity.principalId
//     roleDefinitionId: iotHubTwinContributorRoleId
//   }
// }

