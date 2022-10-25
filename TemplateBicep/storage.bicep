param uniqueSolutionPrefix string
param storageAccountType string
param location string

var storageAccountName = '${uniqueSolutionPrefix}storage'

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-05-01' = {
  name: storageAccountName
  location: location
  kind: storageAccountType
  sku: {
    name: 'Standard_LRS'
  }
}

resource credentialscontainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: '${storageAccount.name}/default/stationcredentials'
}

resource firwareupgradescontainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  name: '${storageAccount.name}/default/fwupgrades'
}

output storageAccountName string = storageAccount.name
