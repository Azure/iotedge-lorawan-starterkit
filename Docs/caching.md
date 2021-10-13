# Caching

## Function

The function is utilizing a [Redis](https://redis.io/) cache to store device related information. It is composed of multiple cache entries:

### LoRaDeviceCache

Stores an instance of type [`DeviceCacheInfo`](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/LoraKeysManagerFacade/DeviceCacheInfo.cs) by DevEUI to keep track of FCntUp, FCntDown, GatewayId per LoRaWAN Network Server (LNS). The cache is used to have a distributed lock in a multi gateway scenario. The info per gateway is stored using the DevEUI to determine what GW is allowed to process a particular message and respond to the sending device.

All the values in this cache are LoRaWAN related and don't require any other information than what we get from the device and the gateway handling a particular message.

This cache needs to be reset, when a device re-joins.

```c#
public class DeviceCacheInfo
{
  public uint FCntUp { get; set; }
  public uint FCntDown { get; set; }
  public string GatewayId { get; set; }
}
```

### LoRaDevAddrCache

The `LoRaDevAddrCache` contains important information from the IoT Hub we require for different scenarios. Most of the information is stored in device twins that are loaded and synchronized on a predefined schedule. Twins queries have strict limits in terms of reads/device and module [Understand Azure IoT Hub quotas and throttling | Microsoft Docs](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-quotas-throttling#operation-throttles). Therefore this cache was put in the middle to handle the higher load we would generate to read out the information stored in IoT Hub.

The cache is organized as a HSET - [HSET – Redis](https://redis.io/commands/hset) - The key being the DevAddr and individual DevEUI as the field. The values are `DevAddrCacheInfo`.

```c#
public class DevAddrCacheInfo : IoTHubDeviceInfo
{
  public string GatewayId { get; set; }
  public DateTime LastUpdatedTwins { get; set; }
  public string NwkSKey { get; set; }
}

public class IoTHubDeviceInfo
{
  public string DevAddr { get; set; }
  public string DevEUI { get; set; }
  public string PrimaryKey { get; set; }
}
```

This cache is automatically being populated on a schedule. We have a function trigger `SyncDevAddrCache` that is triggered on a regular basis (currently every 5min) to validate what synchronization is required.

If the system does warm up, it will trigger a full reload. The full reload fetches all devices from the registry and synchronizes the relevant values from the twins. The sync process, does not synchronize the private key of the device from IoT hub (they will be loaded on request).

The full reload will be performed at most once every 24h (unless the redis cache is completely cleared). The incremental updates do make sure we only load the delta using the timestamps on the desired and reported property:

```c#
var query = $"SELECT * FROM devices where properties.desired.$metadata.$lastUpdated >= '{lastUpdate}' OR properties.reported.$metadata.DevAddr.$lastUpdated >= '{lastUpdate}'";
```

### Join related caching

When we receive OTAA requests, we manage the potential of conflicting with multiple gateways as well with the redis cache. We maintain 2 caches:

1. #### devnonce

   The devnonce keeps track of nonce values sent by the device for a join request. It makes sure the same join request is only handled once by one Gateway. The key is composed of the [DevEUI]:[DevNonce] values. It's evicted after 5min it was added to the cache.

2. #### Join-info

   The Join-info cache contains information required when a new device joins the network. The cache is keyed by [DevEUI]:joininfo and is valid for 60min after initial creation.

   ```c#
   public class JoinInfo
   {
     public string PrimaryKey { get; set; }
     public string DesiredGateway { get; set; }
   }
   ```

   The DesiredGateway is used to determine, if the gateway making the request, is the desired gateway. If the value is not set, the first one to win the race, will handle the join.

   The PrimaryKey is used to create the device connection from the edge gateway to IoT Hub.
