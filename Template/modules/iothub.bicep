param iothubname string
param location string

resource iotHubName 'Microsoft.Devices/IotHubs@2021-07-02' = {
  name: iothubname
  location: location
  sku: {
    capacity: 1
    name: 'S1'
  }
}
