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

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2020-04-01' existing = {
  name: identityId
}

var containerGroupName = '${uniqueSolutionPrefix}containergroup'

resource runPowerShellInline 'Microsoft.Resources/deploymentScripts@2020-10-01' = {
  name: 'runPowerShellInline'
  location: location
  kind: 'AzureCLI'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      identity : {}
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
    arguments: '-arg \\"something\\"'
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
      param([string] $arg)
      $output = \'arg {0}. env var {1}.\' -f $arg,\${Env:var1}
      Write-Output $output

      az iot hub device-identity create --hub-name ${Env:iotHubName}  --device-id ${Env:deviceId}

      $DeploymentScriptOutputs = @{}
      $DeploymentScriptOutputs[\'text\'] = $output
    ''' // or primaryScriptUri: 'https://raw.githubusercontent.com/Azure/azure-docs-bicep-samples/main/samples/deployment-script/inlineScript.ps1'
    supportingScriptUris: []
    timeout: 'PT30M'
    cleanupPreference: 'OnSuccess'
    retentionInterval: 'P1D'
  }
}
