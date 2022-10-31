param location string = resourceGroup().location
param iothubName string
param edgeGatewayName string
param resetPin int
param spiSpeed int
param spiDev int
param utcValue string = utcNow()
param functionAppName string
param region string
param stationEui string
param lnsHostAddress string = 'ws://mylns:5000'
param useAzureMonitorOnEdge bool = true
param logAnalyticsName string
param deployDevice bool
param loraCliUrl string
param version string

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

resource createIothubDevices 'Microsoft.Resources/deploymentScripts@2020-10-01' = {
  name: 'createIothubDevices'
  kind: 'AzureCLI'
  location: location
  properties: {
    forceUpdateTag: utcValue
    azCliVersion: '2.40.0'
    cleanupPreference: 'OnSuccess'
    timeout: 'PT10M'
    retentionInterval: 'P1D'
    environmentVariables: [
      {
        name: 'IOTHUB_RESOURCE_ID'
        value: iotHub.id
      }
      {
        name: 'IOTHUB_CONNECTION_STRING'
        secureValue: 'HostName=${iotHub.name}.azure-devices.net;SharedAccessKeyName=${listKeys(iotHub.id, '2020-04-01').value[0].keyName};SharedAccessKey=${listKeys(iotHub.id, '2020-04-01').value[0].primaryKey}'
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
        name: 'REGION'
        value: toLower(region)
      }
      {
        name: 'MONITORING_ENABLED'
        value: useAzureMonitorOnEdge ? '1' : '0'
      }
      {
        name: 'DEPLOY_DEVICE'
        value: deployDevice ? '1' : '0'
      }
      {
        name: 'LORA_CLI_URL'
        value: loraCliUrl
      }
      {
        name: 'LORA_VERSION'
        value: version
      }
    ]
    scriptContent: loadTextContent('./create_device.sh')
  }
}
