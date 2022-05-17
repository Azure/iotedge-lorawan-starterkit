param uniqueSolutionPrefix string
param location string = resourceGroup().location
param discoveryZipUrl string

module iotHub 'iothub.bicep' = {
  name: 'iothub'
  params: {}
}

module storage 'storage.bicep' = {
  name: 'storage'
  params: {}
}

module function 'function.bicep' = {
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

module discoveryService 'discoveryService.bicep' = {
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
