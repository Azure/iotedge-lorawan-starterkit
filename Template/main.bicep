param uniqueSolutionPrefix string
param location string = resourceGroup().location

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
