param location string = resourceGroup().location
param iothubName string = ''
param edgeGatewayName string = ''
param resetPin int
param spiSpeed int
param spiDev int
param utcValue string = utcNow()
param functionAppName string = ''
param region string
param stationEui string
param lnsHostAddress string = 'ws://mylns:5000'
param gitUsername string = 'Azure'
param useAzureMonitorOnEdge bool = true
param logAnalyticsName string
param deployDevice bool

resource iotHub 'Microsoft.Devices/IotHubs@2021-07-02' existing = {
  name: iothubName
}

// https://stackoverflow.com/questions/69251430/output-newly-created-function-app-key-using-bicep
resource functionApp 'Microsoft.Web/sites@2018-11-01' existing = {
  name: functionAppName
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = {
  name: logAnalyticsName
}

// Create User Defined Identity
resource deviceProvisioningManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: '${resourceGroup().name}-lora-device-provisioning'
  location: location
}

// Create Contributor Assignment for IoT Hub
resource iothubContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, deviceProvisioningManagedIdentity.name, 'iothub-contributor')
  scope: iotHub
  properties: {
    principalId: deviceProvisioningManagedIdentity.properties.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')
    principalType: 'ServicePrincipal'
  }
}

resource createIothubDevices 'Microsoft.Resources/deploymentScripts@2020-10-01' = {
  name: 'createIothubDevices'
  kind: 'AzureCLI'
  location: location
  dependsOn: [ iothubContributorRoleAssignment ]
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${deviceProvisioningManagedIdentity.id}': {}
    }
  }
  properties: {
    forceUpdateTag: utcValue
    azCliVersion: '2.40.0'
    cleanupPreference: 'OnSuccess'
    timeout: 'PT10M'
    retentionInterval: 'P1D'
    environmentVariables: [
      {
        name: 'IOTHUB_NAME'
        value: iotHub.name
      }
      {
        name: 'IOTHUB_RESOURCE_ID'
        value: iotHub.id
      }
      {
        name: 'LOG_ANALYTICS_WORKSPACE_ID'
        value: logAnalytics.properties.customerId
      }
      {
        name: 'LOG_ANALYTICS_SHARED_KEY'
        secureValue: listKeys(logAnalytics.id, '2022-10-01').primarySharedKey
      }
      {
        name : 'RESOURCE_GROUP'
        value: resourceGroup().name
      }
      {
        name : 'EDGE_GATEWAY_NAME'
        value: edgeGatewayName
      }
      {
        name : 'STATION_DEVICE_NAME'
        value: stationEui
      }
      {
        name : 'RESET_PIN'
        value: string(resetPin)
      }
      {
        name : 'SPI_SPEED'
        value: string(spiSpeed)
      }
      {
        name : 'SPI_DEV'
        value: string(spiDev)
      }
      {
        name: 'FACADE_AUTH_CODE'
        secureValue: listkeys('${functionApp.id}/host/default', '2016-08-01').functionKeys.default
      }
      {
        name: 'FACADE_SERVER_URL'
        value: 'https://${functionApp.name}.azurewebsites.net/api'
      }
      {
        name: 'NETWORK'
        value: 'quickstartnetwork'
      }
      {
        name: 'LNS_HOST_ADDRESS'
        value: lnsHostAddress
      }
      {
        name: 'MODULE_CONFIG'
        value: loadTextContent('edgeGatewayManifest.json')
      }
      {
        name: 'REGION'
        value: toLower(region)
      }
      {
        name: 'MONITORING_ENABLED'
        value: useAzureMonitorOnEdge ? '1' : '0'
      }
      {
        name: 'MONITORING_LAYER_CONFIG'
        value: loadTextContent('observabilityLayerManifest.json')
      }
      {
        name: 'DEPLOY_DEVICE'
        value: deployDevice ? '1' : '0'
      }
    ]
    supportingScriptUris: [
      'https://raw.githubusercontent.com/${gitUsername}/iotedge-lorawan-starterkit/dev/Tools/Cli-LoRa-Device-Provisioning/DefaultRouterConfig/EU863.json'
      'https://raw.githubusercontent.com/${gitUsername}/iotedge-lorawan-starterkit/dev/Tools/Cli-LoRa-Device-Provisioning/DefaultRouterConfig/US902.json'
    ]
    scriptContent: loadTextContent('./create_device.sh')
  }
}
