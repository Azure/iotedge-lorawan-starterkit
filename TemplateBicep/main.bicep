param location string = resourceGroup().location

@description('Prefix used for resource names. Should be unique as this will also be used for domain names.')
param uniqueSolutionPrefix string

@description('The name of the Edge gateway')
param edgeGatewayName string

@description('Provision a final LoRa device in the IoT hub in addition to the gateway')
param deployDevice bool

@description('Provide the reset pin value of your gateway. Please refer to the doc if you are unfamiliar with the value')
param resetPin int 

@description('In what region is your gateway deployed?')
@allowed(['EU863', 'US902', 'AS923-1', 'AS923-2', 'AS923-3', 'CN470RP1', 'CN470RP2', 'AU915'])
param region string = 'EU863'

@description('The StationEUI of the sample concentrator device')
param stationEui string = 'AA555A0000000101'

@description('[In Mbps] Custom SPI speed for your gateway, currently only supported for ARM gateways')
@allowed([8,2])
param spiSpeed int = 8

@description('SPI Dev version for x86 based gateway')
@allowed([0,1,2])
param spiDev int = 0

@description('Controls whether observability is set up for IoT edge.')
param useAzureMonitorOnEdge bool = true

@description('Controls whether the standalone discovery service should be deployed.')
param useDiscoveryService bool = false

@description('The Git Username. Default is Azure.')
param gitUsername string = 'Azure'

@description('The LoRaWAN Starter Kit version to use.')
param version string = '2.2.2'

@description('The location of the cli tool to be used for device provisioning.')
param loraCliUrl string = 'https://github.com/Azure/iotedge-lorawan-starterkit/releases/download/v${version}/lora-cli.linux-musl-x64.tar.gz'

module iotHub './iothub.bicep' = {
  name: 'iotHub'
  params: {
    name: '${uniqueSolutionPrefix}hub'
    location: location
  }
}

module storage './storage.bicep' = {
  name: 'storage'
  params: {
    uniqueSolutionPrefix: uniqueSolutionPrefix
    storageAccountType: 'StorageV2'
    location: location
  }
}

module redisCache './redis.bicep' = {
  name: 'redisCache'
  params: {
    uniqueSolutionPrefix: uniqueSolutionPrefix
    location: location
  }
}

module function './function.bicep' = {
  name: 'function'
  params: {
    appInsightName: observability.outputs.appInsightName
    logAnalyticsName: observability.outputs.logAnalyticsName
    uniqueSolutionPrefix: uniqueSolutionPrefix
    useAzureMonitorOnEdge: useAzureMonitorOnEdge
    hostingPlanLocation: location
    redisCacheName: redisCache.outputs.redisCacheName
    iotHubName: iotHub.outputs.iotHubName
    storageAccountName: storage.outputs.storageAccountName
    gitUsername: gitUsername
    version: version
  }
}

module observability './observability.bicep' = {
  name: 'observability'
  params: {
    prefix: uniqueSolutionPrefix
    iotHubName: iotHub.outputs.iotHubName
    location: location
    useAzureMonitorOnEdge: useAzureMonitorOnEdge
  }
}

module discoveryService './discoveryService.bicep' = if (useDiscoveryService) {
  name: 'discoveryService'
  params: {
    appInsightName: observability.outputs.appInsightName
    version: version
    gitUsername: gitUsername
    iotHubName: iotHub.outputs.iotHubName
    uniqueSolutionPrefix: uniqueSolutionPrefix
    location: location
  }
}

module createDevices 'devices.bicep' = {
  name: 'createDevices'
  params: {
    location: location
    deployDevice: deployDevice
    logAnalyticsName: observability.outputs.logAnalyticsName
    functionAppName: function.outputs.functionName
    iothubName: iotHub.outputs.iotHubName
    edgeGatewayName: edgeGatewayName
    resetPin: resetPin
    region: region
    stationEui: stationEui
    spiSpeed: spiSpeed
    spiDev: spiDev
    loraCliUrl: loraCliUrl
    version: version
  }
}
