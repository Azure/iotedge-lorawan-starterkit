@description('Storage account name.')
param storageAccountName string
@description('Storage account type.')
param storageAccountType string
@description('Storage account location.')
param location string

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-05-01' = {
  name: storageAccountName
  location: location
  kind: storageAccountType
  sku: {
    name: 'Standard_LRS'
  }
}

param credentialsContainerName string
var FormattedCredentialsContainerName = '${storageAccount.name}/default/${credentialsContainerName}'
resource credentialscontainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: FormattedCredentialsContainerName
}

param firmwareUpgradesContainerName string
var FormattedFirmwareUpgradesContainerName = '${storageAccount.name}/default/${firmwareUpgradesContainerName}'
resource firwareupgradescontainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: FormattedFirmwareUpgradesContainerName
}

output storageAccountName string = storageAccount.name
