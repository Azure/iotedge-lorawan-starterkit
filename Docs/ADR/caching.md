# Caching

## Function

The function is utilizing a [Redis](https://redis.io/) cache to store device related information. It is composed of multiple cache entries:

### LoRaDeviceCache

Stores an instance of type `DeviceCacheInfo` by DevEUI to keep track of FCntUp, FCntDown, GatewayId per LoRaWAN Network Server (LNS). The cache is used to have a distributed lock in a multi gateway scenario. The info per gateway is stored using the DevEUI to determine what GW is allowed to process a particular message and respond to the sending device. 

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

This cache is 