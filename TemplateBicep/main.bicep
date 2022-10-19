

param uniqueSolutionPrefix string
param location string = resourceGroup().location
param discoveryZipUrl string

module iotHub 'modules/iothub.bicep' = {
  name: 'iotHub'
  params: {
    name: '${uniqueSolutionPrefix}hub'
    location: location
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    storageAccountName: '${uniqueSolutionPrefix}storage'
    storageAccountType: 'StorageV2'
    credentialsContainerName: 'stationcredentials'
    firmwareUpgradesContainerName: 'fwupgrades'
    location: location
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
    logAnalyticsName: observability.outputs.logAnalyticsName
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
    iotHubName: iotHub.outputs.iotHubName
    uniqueSolutionPrefix: uniqueSolutionPrefix
    location: location
  }
}

