
var storageAccountName = '${uniqueSolutionPrefix}storage'
var storageAccountType = 'StorageV2'
var credentialsContainerName = 'stationcredentials'
var firmwareUpgradesContainerName = 'fwupgrades'
var iotHubName = '${uniqueSolutionPrefix}hub'

param uniqueSolutionPrefix string
param location string = resourceGroup().location
param discoveryZipUrl string

module iotHub 'modules/iothub.bicep' = {
  name: 'iotHub'
  params: {
    name: iotHubName
    location: location
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    storageAccountName: storageAccountName
    storageAccountType: storageAccountType
    credentialsContainerName: credentialsContainerName
    firmwareUpgradesContainerName: firmwareUpgradesContainerName

  }
}

module redisCache 'modules/redis.bicep' = {
  name: 'redisCache'
  params: {
    uniqueSolutionPrefix: uniqueSolutionPrefix
    location: location
  }
}

module function 'modules/function.bicep' = {
  name: 'function'
  params: {
    appInsightName: observability.outputs.appInsightName
    logAnalyticsName: observability.outputs.logAnalyticName
    deployDevice: true
    uniqueSolutionPrefix: uniqueSolutionPrefix
    useAzureMonitorOnEdge: true
    hostingPlanLocation: location
    redisCacheName: redisCache.outputs.redisCacheName
    iotHubName: iotHub.outputs.iotHubName
    storageAccountName: storage.outputs.storageAccountName
  }
}

module observability 'modules/observability.bicep' = {
  name: 'observability'
  params: {
    prefix: uniqueSolutionPrefix
    iotHubName: iotHub.outputs.iotHubName
    location: location
  }
}

module discoveryService 'modules/discoveryService.bicep' = {
  name: 'discoveryService'
  params: {
    appInsightName: observability.outputs.appInsightName
    discoveryZipUrl: discoveryZipUrl
    iotHubHostName: reference(resourceId('Microsoft.Devices/IoTHubs', iotHubName), providers('Microsoft.Devices', 'IoTHubs').apiVersions[0]).hostName
    iotHubName: iotHub.outputs.iotHubName
    uniqueSolutionPrefix: uniqueSolutionPrefix
    location: location
  }
}

