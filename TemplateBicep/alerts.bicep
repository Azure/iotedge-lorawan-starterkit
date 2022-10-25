param iothubName string
param appInsightsName string

@description('Dynamic Threshold sensitivity (High, Medium, Low)')
param alertSensitivity string = 'Medium'

@description('Dynamic Threshold Failing Periods configurations https://docs.microsoft.com/azure/azure-monitor/alerts/alerts-dynamic-thresholds#what-do-the-advanced-settings-in-dynamic-thresholds-mean')
param evaluationPeriods int = 4

@description('Dynamic Threshold Failing Periods configurations https://docs.microsoft.com/azure/azure-monitor/alerts/alerts-dynamic-thresholds#what-do-the-advanced-settings-in-dynamic-thresholds-mean')
param failingPeriods int = 4

@description('Metric namespace for the alert. Default is LoRaWan')
param metricNamespace string = 'LoRaWan'

@description('Every hour (5 mins, 15 mins, 30 mins, etc.)')
param evaluationFrequency string = 'PT1H'

@description('1 hour (5 mins, 15 mins, 30 mins, etc.)')
param aggregationPeriod string = 'PT1H'

resource iotHub 'Microsoft.Devices/IotHubs@2021-07-02' existing = {
  name: iothubName
}

resource appInsight 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource highUpstreamMessageLatencyAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'High Upstream Message Latency'
  location: 'global'
  properties: {
    description: 'High device message processing time (throughput)'
    severity: 2
    enabled: true
    evaluationFrequency: evaluationFrequency
    windowSize: aggregationPeriod
    autoMitigate: true
    targetResourceType: 'microsoft.insights/components'
    actions: []
    scopes: [
      appInsight.id
    ]
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
      allOf: [
        {
          alertSensitivity: alertSensitivity
          failingPeriods: {
            numberOfEvaluationPeriods: evaluationPeriods
            minFailingPeriodsToAlert: failingPeriods
          }
          name: 'Metric1'
          metricNamespace: metricNamespace
          metricName: 'D2CMessageDeliveryLatency'
          dimensions: [
            {
              name: 'GatewayId'
              operator: 'Include'
              values: [ '*' ]
            }
          ]
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          criterionType: 'DynamicThresholdCriterion'
          skipMetricValidation: true
        }
      ]
    }
  }
}


resource highReceiveWindowMissesAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'High Receive Window Misses'
  location: 'global'
  properties: {
    description: 'High receive window misses (throughput)'
    severity: 2
    enabled: true
    evaluationFrequency: evaluationFrequency
    windowSize: aggregationPeriod
    autoMitigate: true
    targetResourceType: 'microsoft.insights/components'
    actions: []
    scopes: [
      appInsight.id
    ]
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
      allOf: [
        {
          alertSensitivity: alertSensitivity
          failingPeriods: {
            numberOfEvaluationPeriods: evaluationPeriods
            minFailingPeriodsToAlert: failingPeriods
          }
          name: 'Metric1'
          metricNamespace: metricNamespace
          metricName: 'ReceiveWindowMisses'
          dimensions: [
            {
              name: 'GatewayId'
              operator: 'Include'
              values: [ '*' ]
            }
          ]
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          criterionType: 'DynamicThresholdCriterion'
          skipMetricValidation: true
        }
      ]
    }
  }
}

resource unhandledExceptionsAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'Unhandled Exceptions'
  location: 'global'
  properties: {
    description: 'High error count (correctness)'
    severity: 2
    enabled: true
    evaluationFrequency: evaluationFrequency
    windowSize: aggregationPeriod
    autoMitigate: true
    targetResourceType: 'microsoft.insights/components'
    actions: []
    scopes: [
      appInsight.id
    ]
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
      allOf: [
        {
          alertSensitivity: alertSensitivity
          failingPeriods: {
            numberOfEvaluationPeriods: evaluationPeriods
            minFailingPeriodsToAlert: failingPeriods
          }
          name: 'Metric1'
          metricNamespace: metricNamespace
          metricName: 'UnhandledExceptions'
          operator: 'GreaterThan'
          timeAggregation: 'Count'
          criterionType: 'DynamicThresholdCriterion'
          skipMetricValidation: true
        }
      ]
    }
  }
}

resource highDownstreamMessageAbandonedAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'High Downstream Messages Abandoned Number'
  location: 'global'
  properties: {
    description: 'High device messages abandoned number (correctness, throughput)'
    severity: 2
    enabled: true
    evaluationFrequency: evaluationFrequency
    windowSize: aggregationPeriod
    autoMitigate: true
    targetResourceType: 'microsoft.devices/iothubs'
    actions: []
    scopes: [
      iotHub.id
    ]
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
      allOf: [
        {
          alertSensitivity: alertSensitivity
          failingPeriods: {
            numberOfEvaluationPeriods: evaluationPeriods
            minFailingPeriodsToAlert: failingPeriods
          }
          name: 'Metric1'
          metricNamespace: 'Microsoft.Devices/IotHubs'
          metricName: 'c2d.commands.egress.abandon.success'
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          criterionType: 'DynamicThresholdCriterion'
          skipMetricValidation: true
        }
      ]
    }
  }
}

resource highDownstreamMessageRejectAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'High Downstream Messages Rejected Number'
  location: 'global'
  properties: {
    description: 'High device messages rejected number (correctness, throughput)'
    severity: 2
    enabled: true
    evaluationFrequency: evaluationFrequency
    windowSize: aggregationPeriod
    autoMitigate: true
    targetResourceType: 'microsoft.devices/iothubs'
    actions: []
    scopes: [
      iotHub.id
    ]
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
      allOf: [
        {
          alertSensitivity: alertSensitivity
          failingPeriods: {
            numberOfEvaluationPeriods: evaluationPeriods
            minFailingPeriodsToAlert: failingPeriods
          }
          name: 'Metric1'
          metricNamespace: 'Microsoft.Devices/IotHubs'
          metricName: 'c2d.commands.egress.reject.success'
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          criterionType: 'DynamicThresholdCriterion'
          skipMetricValidation: true
        }
      ]
    }
  }
}

resource highUpstreamMessageLostRatioAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'High Upstream Messages Lost Ratio'
  location: 'global'
  properties: {
    description: 'High Upstream Messages Lost Ratio (Correctness) (D2CMessagesReceived/D2CMessagesDelivered)'
    severity: 2
    enabled: true
    evaluationFrequency: evaluationFrequency
    windowSize: aggregationPeriod
    autoMitigate: true
    targetResourceType: 'microsoft.insights/components'
    actions: []
    scopes: [
      appInsight.id
    ]
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
      allOf: [
        {
          alertSensitivity: alertSensitivity
          failingPeriods: {
            numberOfEvaluationPeriods: evaluationPeriods
            minFailingPeriodsToAlert: failingPeriods
          }
          name: 'Metric1'
          metricNamespace: metricNamespace
          metricName: 'D2CMessagesLostRatio'
          dimensions: [
            {
              name: 'GatewayId'
              operator: 'Include'
              values: [ '*' ]
            }
          ]
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          criterionType: 'DynamicThresholdCriterion'
          skipMetricValidation: true
        }
      ]
    }
  }
}
