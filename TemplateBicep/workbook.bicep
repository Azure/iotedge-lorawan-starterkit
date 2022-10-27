
param location string
param iothubName string
param appInsightsName string

var workbookDisplayName = 'StarterKitWorkbook'

resource iotHub 'Microsoft.Devices/IotHubs@2021-07-02' existing = {
  name: iothubName
}

resource appInsight 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}


var workbookContent = replace(replace(replace(loadTextContent('./starter-kit-workbook.json'), '{{subscriptionResourceId}}', subscription().id), '{{appInsightsResourceId}}', appInsight.id), '{{iotHubResourceId}}', iotHub.id)

resource workbook 'Microsoft.Insights/workbooks@2022-04-01' = {
  name: guid(workbookDisplayName)
  location: location
  kind: 'shared'
  properties: {
    displayName: workbookDisplayName
    serializedData: workbookContent
    version: '1.0'
    category: 'workbook'
    sourceId: iotHub.id
  }
}
