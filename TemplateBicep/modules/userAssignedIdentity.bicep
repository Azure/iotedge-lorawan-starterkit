param location string = resourceGroup().location
param iotHubName string
param uniqueSolutionPrefix string

var userAssignedIdentityName = '${uniqueSolutionPrefix}identity'
var contributorName = '${uniqueSolutionPrefix}contributor'

resource iotHub 'Microsoft.Devices/IotHubs@2021-03-31' existing = {
  name: iotHubName
}

// create role assignment
//var IOT_HUB_REGISTRY_CONTRIBUTOR_USER_ROLE_GUID = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4ea46cd5-c1b2-4a8e-910b-273211f9ce47')
var IOT_HUB_REGISTRY_CONTRIBUTOR_USER_ROLE_GUID = '4ea46cd5-c1b2-4a8e-910b-273211f9ce47'

// create user assigned managed identity
resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: userAssignedIdentityName
  location: location
}

resource iotHubRegistryContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: contributorName
  scope: iotHub
  properties: {
    principalId: userAssignedIdentity.properties.principalId
    roleDefinitionId: IOT_HUB_REGISTRY_CONTRIBUTOR_USER_ROLE_GUID
  }
}

output identityId string = userAssignedIdentity.id
