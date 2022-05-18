var iotHubName = '${uniqueSolutionPrefix}hub'
var storageAccountName = '${uniqueSolutionPrefix}storage'
var credentialsContainerName = 'stationcredentials'
var formattedCredentialsContainerName = '${storageAccountName}/default/${credentialsContainerName}'
var firmwareUpgradesContainerName = 'fwupgrades'
var formattedFirmwareUpgradesContainerName = '${storageAccountName}/default/${firmwareUpgradesContainerName}'
var redisCacheName = '${uniqueSolutionPrefix}redis'
var hostingPlanName = '${uniqueSolutionPrefix}plan'
var appInsightName = '${uniqueSolutionPrefix}insight'
var functionName = '${uniqueSolutionPrefix}function'
var iotHubOwnerPolicyName = 'iothubowner'
var logAnalyticsName = '${uniqueSolutionPrefix}insight'
var functionZipBinary = 'https://github.com/${gitUsername}/iotedge-lorawan-starterkit/releases/download/v${version}/function-${version}.zip'
var discoveryServiceZipBinary = 'https://github.com/${gitUsername}/iotedge-lorawan-starterkit/releases/download/v${version}/discoveryservice-${version}.zip'

@description('Unique prefix that will be added on all deployed resources.')
param uniqueSolutionPrefix string

@description('The Azure location where resources will be deployed. Default is resource group location.')
param location string = resourceGroup().location

@description('Provision a final LoRa device in the IoT hub in addition to the gateway.')
param deployDevice bool = true

@description('The Git Username. Default is Azure.')
param gitUsername string = 'Azure'

@description('Hosting plan SKU. Default is Y1.')
param hostingPlanSkuName string = 'Y1'

@description('Hosting Plan SKU tiers. Default value is Dynamic.')
param hostingPlanSkuTier string = 'Dynamic'

@description('The Git version to use. Default is 2.1.0.')
param version string = '2.1.0'

@description('Controls whether observability is set up for IoT edge.')
param useAzureMonitorOnEdge bool = true

resource iotHub 'Microsoft.Devices/IotHubs@2021-07-02' = {
  name: iotHubName
  location: location
  sku: {
    capacity: 1
    name: 'S1'
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
}

resource credentialscontainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-06-01' = {
  name: formattedCredentialsContainerName
  dependsOn: [
    storageAccount
  ]
}

resource firwareupgradescontainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-06-01' = {
  name: formattedFirmwareUpgradesContainerName
  dependsOn: [
    storageAccount
  ]
}

resource redisCache 'Microsoft.Cache/Redis@2019-07-01' = {
  location: location
  name: redisCacheName
  properties: {
    sku: {
      capacity: 0
      family: 'C'
      name: 'Basic'
    }
  }
}

resource hostingPlan 'Microsoft.Web/serverfarms@2020-12-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: hostingPlanSkuName
    tier: hostingPlanSkuTier
  }
}

resource appInsightsComponents 'Microsoft.Insights/components@2020-02-02-preview' = {
  name: appInsightName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2020-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'Free'
    }
  }
}

resource azureFunction 'Microsoft.Web/sites@2020-12-01' = {
  name: functionName
  location: location
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
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys(storageAccount.id, '2019-06-01').keys[0].value}'
        }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys(storageAccount.id, '2019-06-01').keys[0].value}'
        }
        {
          name: 'AzureWebJobsSecretStorageType'
          value: 'Files'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys(storageAccount.id, '2019-06-01').keys[0].value}'
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
          value: reference(appInsightsComponents.id, '2015-05-01').InstrumentationKey
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
          value: useAzureMonitorOnEdge ? reference(logAnalyticsWorkspace.id).customerId : ''
        }
        {
          name: 'LOG_ANALYTICS_WORKSPACE_KEY'
          value: useAzureMonitorOnEdge ? listKeys(logAnalyticsWorkspace.id, '2021-06-01').primarySharedKey : ''
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

module azureMonitorAlerts 'modules/azureMonitorAlerts.json' = {
  name: 'azureMonitorAlerts'
  params: {
    appInsightName: appInsightName
    iotHubName: iotHubName
  }
  dependsOn: [
    appInsightsComponents
    iotHub
  ]
}

module discoveryService 'modules/discoveryService.bicep' = {
  name: 'discoveryService'
  params: {
    iotHubHostName: '${iotHub.name}.azure-devices.net'
    uniqueSolutionPrefix: uniqueSolutionPrefix
    appInsightName:  appInsightsComponents.name
    iotHubName: iotHub.name
    discoveryZipUrl: discoveryServiceZipBinary
    location: location
  }
}

module workbook 'modules/workbook.json' = {
  name: 'workbook'
  params: {
    appInsightsResourceId: appInsightsComponents.id
    iotHubResourceId: iotHub.id
  }
}

module createEdgeDevice 'modules/createEdgeDevice.json' = {
  name: 'createEdgeDevice'
  params: {
    resetPin: 1234
    solutionPrefix: uniqueSolutionPrefix
    edgeGatewayName: 'TODO'
    spiSpeed: 2
  }
  dependsOn: [
    azureFunction
  ]
}
