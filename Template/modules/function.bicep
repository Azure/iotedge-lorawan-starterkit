@description('Provision a final LoRa device in the IoT hub in addition to the gateway.')
param deployDevice bool

@description('The Git Username. Default is Azure.')
param gitUsername string = 'Azure'

@description('The Azure location where to create the hosting plan. Default value is resource group location.')
param hostingPlanLocation string = resourceGroup().location

@description('Hosting plan SKU. Default is Y1.')
param hostingPlanSkuName string = 'Y1'

@description('Hosting Plan SKU tiers. Default value is Dynamic.')
param hostingPlanSkuTier string = 'Dynamic'

@description('Prefix used for resource names. Should be unique as this will also be used for domain names.')
param uniqueSolutionPrefix string

@description('Controls whether observability is set up for IoT Edge.')
param useAzureMonitorOnEdge bool

@description('The Git version to use. Default is 2.1.0.')
param version string = '2.1.0'

var appInsightName = '${uniqueSolutionPrefix}insight'
var functionName = '${uniqueSolutionPrefix}function'
var functionZipBinary = 'https://github.com/${gitUsername}/iotedge-lorawan-starterkit/releases/download/v${version}/function-${version}.zip'
var hostingPlanName = '${uniqueSolutionPrefix}plan'
var iotHubName = '${uniqueSolutionPrefix}hub'
var iotHubOwnerPolicyName = 'iothubowner'
var logAnalyticsName = '${uniqueSolutionPrefix}log'
var redisCacheName = '${uniqueSolutionPrefix}redis'
var storageAccountName = '${uniqueSolutionPrefix}storage'

// RESOURCES DEPENDENCIES

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightName
}

resource iotHub 'Microsoft.Devices/IotHubs@2021-07-02' existing = {
  name: iotHubName
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2021-06-01' existing = {
  name: logAnalyticsName
}

resource redisCache 'Microsoft.Cache/Redis@2019-07-01' existing = {
  name: redisCacheName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' existing = {
  name: storageAccountName
}

// FUNCTION DEPLOYMENT RESOURCE

resource hostingPlan 'Microsoft.Web/serverfarms@2020-12-01' = {
  name: hostingPlanName
  location: hostingPlanLocation
  sku: {
    name: hostingPlanSkuName
    tier: hostingPlanSkuTier
  }
}

resource azureFunction 'Microsoft.Web/sites@2020-12-01' = {
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
          connectionString:'HostName=${iotHub.name}.azure-devices.net;SharedAccessKeyName=${iotHubOwnerPolicyName};SharedAccessKey=${listKeys(resourceId('Microsoft.Devices/IotHubs/IotHubKeys', iotHub.name, iotHubOwnerPolicyName), '2017-01-19').primaryKey}'
        }
        {
          name: 'RedisConnectionString'
          type: 'Custom'
          connectionString: '${redisCache.name}.redis.cache.windows.net,abortConnect=false,ssl=true,password=${listKeys(redisCache.name, '2015-08-01').primaryKey}'
        }
      ]
      appSettings: [
        {
          name: 'AzureWebJobsDashboard'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys(storageAccount.id, '2019-06-01').key1}'
        }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys(storageAccount.id, '2019-06-01').key1}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys(storageAccount.id, '2019-06-01').key1}'
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
          name: 'DEPLOY_DEVICE'
          value: string(deployDevice)
        }
        {
          name: 'DEVICE_CONFIG_LOCATION'
          value: 'https://raw.githubusercontent.com/${gitUsername}/iotedge-lorawan-starterkit/v${version}/Template/deviceConfiguration.json'
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
          name: 'OBSERVABILITY_CONFIG_LOCATION'
          value: 'https://raw.githubusercontent.com/${gitUsername}/iotedge-lorawan-starterkit/v${version}/Template/observabilityConfiguration.json'
        }
        {
          name: 'IOT_HUB_RESOURCE_ID'
          value: iotHub.id
        }
        {
          name: 'LOG_ANALYTICS_WORKSPACE_ID'
          value: useAzureMonitorOnEdge ? reference(logAnalytics.id).customerId : ''
        }
        {
          name: 'LOG_ANALYTICS_WORKSPACE_KEY'
          value: useAzureMonitorOnEdge ? listKeys(logAnalytics.id, '2021-06-01').primarySharedKey : ''
        }
        {
          name: 'EU863_CONFIG_LOCATION'
          value: 'https://raw.githubusercontent.com/${gitUsername}/iotedge-lorawan-starterkit/v${version}/Tools/Cli-LoRa-Device-Provisioning/DefaultRouterConfig/EU863.json'
        }
        {
          name: 'US902_CONFIG_LOCATION'
          value: 'https://raw.githubusercontent.com/${gitUsername}/iotedge-lorawan-starterkit/v${version}/Tools/Cli-LoRa-Device-Provisioning/DefaultRouterConfig/US902.json'
        }
      ]
    }
  }
}

