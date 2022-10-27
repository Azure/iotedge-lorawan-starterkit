@description('The Azure location where to create the hosting plan. Default value is resource group location.')
param location string

@description('Prefix used for resource names. Should be unique as this will also be used for domain names.')
param uniqueSolutionPrefix string

var redisCacheName = '${uniqueSolutionPrefix}redis'

resource redisCache 'Microsoft.Cache/Redis@2022-06-01' = {
  location: location
  name: redisCacheName
  properties: {
    sku: {
      capacity: 0
      family: 'C'
      name: 'Basic'
    }
  }
}

output redisCacheName string = redisCache.name
