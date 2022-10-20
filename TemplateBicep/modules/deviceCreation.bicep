// See: https://learn.microsoft.com/en-us/cli/azure/iot/hub/device-identity?view=azure-cli-latest#az-iot-hub-device-identity-create
// See: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deployment-script-bicep

param location string = resourceGroup().location
param storageAccountName string
param uniqueSolutionPrefix string
param iotHubName string
param identityId string

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-05-01' existing = {
  name: storageAccountName
}

resource iotHub 'Microsoft.Devices/IotHubs@2021-03-31' existing = {
  name: iotHubName
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' existing = {
  name: identityId
}

var containerGroupName = '${uniqueSolutionPrefix}containergroup'

resource runPowerShellInline 'Microsoft.Resources/deploymentScripts@2020-10-01' = {
  name: '${uniqueSolutionPrefix}runPowerShellInline'
  location: location
  kind: 'AzureCLI'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {} 
    }
  }
  properties: {
    forceUpdateTag: '1'
    containerSettings: {
      containerGroupName: containerGroupName
    }
    storageAccountSettings: {
      storageAccountName: storageAccount.name
      storageAccountKey: '${listKeys(storageAccount.id, storageAccount.apiVersion).keys[0].value}'
    }
    azCliVersion: '2.0.80'
    environmentVariables: [
      {
        name: 'rgName'
        value: resourceGroup().name
      }
      {
        name: 'iotHubName'
        value: iotHub.name
      }
      {
        name: 'deviceId'
        value: '47AAC86800430028'
      }
    ]
    scriptContent: '''
      az iot hub device-identity create --hub-name ${Env:iotHubName} --device-id ${Env:deviceId}
    ''' 
    // or primaryScriptUri: 'https://raw.githubusercontent.com/Azure/azure-docs-bicep-samples/main/samples/deployment-script/inlineScript.ps1'
    supportingScriptUris: []
    timeout: 'PT30M'
    cleanupPreference: 'OnSuccess'
    retentionInterval: 'P1D'
  }
}
