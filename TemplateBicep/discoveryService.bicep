param location string = resourceGroup().location
param sku string = 'B1'
param version string
param iotHubName string
param appInsightName string
param roleNameGuid string = guid(resourceGroup().id, 'twincontributor')
param uniqueSolutionPrefix string
param gitUsername string

var webAppName = '${uniqueSolutionPrefix}discovery'
var hostingPlanName = '${webAppName}plan'
var aspNetCoreUrls = 'http://0.0.0.0:80;https://0.0.0.0:443'

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightName
}

resource iotHub 'Microsoft.Devices/IotHubs@2021-07-02' existing = {
  name: iotHubName
}

resource hostingPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: sku
  }
}

var discoveryZipUrl = 'https://github.com/${gitUsername}/iotedge-lorawan-starterkit/releases/download/v${version}/discoveryservice-${version}.zip'

resource webApp 'Microsoft.Web/sites@2022-03-01' = {
  name: webAppName
  location: location
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'IotHubHostName'
          value: iotHub.properties.hostName
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
          value: reference(appInsights.id, appInsights.apiVersion).InstrumentationKey
        }
      ]
      webSocketsEnabled: true
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

var iotHubTwinContributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '494bdba2-168f-4f31-a0a1-191d2f7c028c')
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: roleNameGuid
  scope: iotHub
  properties: {
    principalId: webApp.identity.principalId
    roleDefinitionId: iotHubTwinContributorRoleId
    principalType: 'ServicePrincipal'
  }
}

