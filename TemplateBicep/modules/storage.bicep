@description('Storage account name.')
param storageAccountName string
@description('Storage account type.')
param storageAccountType string
@description('Storage account location.')
param location string

resource storageaccount 'Microsoft.Storage/storageAccounts@2022-05-01' = {
  name: storageAccountName
  location: location
  kind: storageAccountType
  sku: {
    name: 'Standard_LRS'
  }
}

param credentialsContainerName string
var FormattedCredentialsContainerName = '${storageAccountName}/default/${credentialsContainerName}'
resource credentialscontainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: FormattedCredentialsContainerName
  dependsOn: [
    storageaccount
  ]
}

param firmwareUpgradesContainerName string
var FormattedFirmwareUpgradesContainerName = '${storageAccountName}/default/${firmwareUpgradesContainerName}'
resource firwareupgradescontainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: FormattedFirmwareUpgradesContainerName
  dependsOn: [
    storageaccount
  ]
}

output storageAccountName string = storageaccount.name
