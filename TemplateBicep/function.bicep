param gitUsername string
param version string

param hostingPlanLocation string = resourceGroup().location
@description('Hosting plan SKU. Default is Y1.')
param hostingPlanSkuName string = 'Y1'
@description('Hosting Plan SKU tiers. Default value is Dynamic.')
param hostingPlanSkuTier string = 'Dynamic'
param uniqueSolutionPrefix string

param useAzureMonitorOnEdge bool
param redisCacheName string
param iotHubName string
param storageAccountName string
param logAnalyticsName string
param appInsightName string

var functionName = '${uniqueSolutionPrefix}function'
var functionZipBinary = 'https://github.com/${gitUsername}/iotedge-lorawan-starterkit/releases/download/v${version}/function-${version}.zip'
var hostingPlanName = '${uniqueSolutionPrefix}plan'
var iotHubOwnerPolicyName = 'iothubowner'

// RESOURCES DEPENDENCIES

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightName
}

resource redisCache 'Microsoft.Cache/redis@2022-06-01' existing = {
  name: redisCacheName
}

resource iotHub 'Microsoft.Devices/IotHubs@2021-07-02' existing = {
  name: iotHubName
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = {
  name: logAnalyticsName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-05-01' existing = {
  name: storageAccountName
}

// FUNCTION DEPLOYMENT RESOURCE

resource hostingPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: hostingPlanName
  location: hostingPlanLocation
  sku: {
    name: hostingPlanSkuName
    tier: hostingPlanSkuTier
  }
}

resource azureFunction 'Microsoft.Web/sites@2022-03-01' = {
  name: functionName
  location: hostingPlanLocation
  kind: 'functionapp'
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      connectionStrings: [
        {
          name: 'IoTHubConnectionString'
          type: 'Custom'
          connectionString:'HostName=${iotHub.name}.azure-devices.net;SharedAccessKeyName=${iotHubOwnerPolicyName};SharedAccessKey=${listKeys(resourceId('Microsoft.Devices/IotHubs/IotHubKeys', iotHub.name, iotHubOwnerPolicyName), iotHub.apiVersion).primaryKey}'
        }
        {
          name: 'RedisConnectionString'
          type: 'Custom'
          connectionString: '${redisCache.name}.redis.cache.windows.net,abortConnect=false,ssl=true,password=${listKeys(redisCache.id, redisCache.apiVersion).primaryKey}'
        }
      ]
      appSettings: [
        {
          name: 'AzureWebJobsDashboard'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys(storageAccount.id, storageAccount.apiVersion).keys[0].value}'
        }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys(storageAccount.id, storageAccount.apiVersion).keys[0].value}'
        }
        {
          name: 'AzureWebJobsSecretStorageType'
          value: 'Files'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys(storageAccount.id, storageAccount.apiVersion).keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionName)
        }
        {
          name: 'FACADE_HOST_NAME'
          value: toLower(functionName)
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: reference(appInsights.id, '2015-05-01').InstrumentationKey
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: functionZipBinary
        }
        {
          name: 'IOT_HUB_RESOURCE_ID'
          value: iotHub.id
        }
        {
          name: 'LOG_ANALYTICS_WORKSPACE_ID'
          value: (useAzureMonitorOnEdge ? logAnalytics.properties.customerId : '')
        }
        {
          name: 'LOG_ANALYTICS_WORKSPACE_KEY'
          value: useAzureMonitorOnEdge ? listKeys(logAnalytics.id, '2022-10-01').primarySharedKey : ''
        }
      ]
    }
  }
}

output functionName string = azureFunction.name
