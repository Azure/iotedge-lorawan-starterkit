param storageAccountName string
param storageAccountType string
resource storageaccount 'Microsoft.Storage/storageAccounts@2015-06-15' = {
  name: storageAccountName
  location: resourceGroup().location
  kind: storageAccountType
  sku: {
    name: 'Premium_LRS'
  }
}

param credentialsContainerName string
param FormattedCredentialsContainerName string = '${storageAccountName}/default/${credentialsContainerName}'
resource credentialscontainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-06-01' = {
  name: FormattedCredentialsContainerName
  dependsOn: [
    storageaccount
  ]
}

param firmwareUpgradesContainerName string
param FormattedFirmwareUpgradesContainerName string = '${storageAccountName}/default/${firmwareUpgradesContainerName}'
resource firwareupgradescontainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-06-01' = {
  name: FormattedFirmwareUpgradesContainerName
  dependsOn: [
    storageaccount
  ]
}
