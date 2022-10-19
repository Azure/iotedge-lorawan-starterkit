param prefix string
param iotHubName string
param location string

resource iotHub 'Microsoft.Devices/IotHubs@2021-07-02' existing = {
  name: iotHubName
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${prefix}analytics'
  location: location
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
    workspaceCapping: {
      dailyQuotaGb: 10
    }
    features: {
      searchVersion: 1
    }
  }
}

resource appInsight 'Microsoft.Insights/components@2020-02-02' = {
  name: '${prefix}insight'
  kind: 'web'
  location: location
  properties: {
    WorkspaceResourceId: logAnalytics.id
    Application_Type: 'web'
  }
}

module workbook './workbook.bicep' = {
  name: 'workbook'
  params: {
    appInsightsName: appInsight.name
    iothubName: iotHub.name
    location: location
  }
}

module azureMonitorAlerts './alerts.bicep' = {
  name: 'azureMonitorAlerts'
  params: {
    appInsightsName: appInsight.name
    iothubName: iotHub.name
  }
}

output appInsightName string = appInsight.name

output logAnalyticsName string = logAnalytics.name
