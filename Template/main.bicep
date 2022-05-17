param uniqueSolutionPrefix string
param location string = resourceGroup().location
param discoveryZipUrl string

module iotHub 'modules/iothub.bicep' = {
  name: 'iothub'
  params: {}
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {}
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
    iotHub,
    storage
  ]
}

module discoveryService 'modules/discoveryService.bicep' = {
  name: 'discoveryService'
  params: {
    appInsightName: '${uniqueSolutionPrefix}insight'
    discoveryZipUrl: discoveryZipUrl
    iotHubHostName: 'TODO'
    iotHubName: '${uniqueSolutionPrefix}hub'
    webAppName: 'TODO'
    location: location
  }
}
