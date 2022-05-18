var iotHubName = '${uniqueSolutionPrefix}hub'
var storageAccountName = '${uniqueSolutionPrefix}storage'
var storageAccountType = 'Standard_LRS'
var credentialsContainerName = 'stationcredentials'
var firmwareUpgradesContainerName = 'fwupgrades'

param uniqueSolutionPrefix string
param location string = resourceGroup().location
param discoveryZipUrl string

module iotHub 'modules/iothub.bicep' = {
  name: 'iothub'
  params: {
    iothubname: iotHubName
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
    deployDevice: true
    uniqueSolutionPrefix: uniqueSolutionPrefix
    useAzureMonitorOnEdge:true
    hostingPlanLocation: location
  }
  dependsOn: [
    iotHub
    storage
    redisCache
  ]
}

module discoveryService 'modules/discoveryService.bicep' = {
  name: 'discoveryService'
  params: {
    appInsightName: '${uniqueSolutionPrefix}insight'
    discoveryZipUrl: discoveryZipUrl
    iotHubHostName: reference(resourceId('Microsoft.Devices/IoTHubs', iotHubName), providers('Microsoft.Devices', 'IoTHubs').apiVersions[0]).hostName
    iotHubName: '${uniqueSolutionPrefix}hub'
    uniqueSolutionPrefix: uniqueSolutionPrefix
    location: location
  }
}
