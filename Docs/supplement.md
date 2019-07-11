
# Supplement Reference Guide

The aim of this reference guide is to provide quick access to common configuration settings, setup and installation tips and troubleshooting advise to help and run the LoRaWAN starter kit smoothly.

## How to use a Proxy Server to connect your Concentrator to Azure

This is an optional configuration that should only be executed if your concentrator needs to use a proxy server to communicate with Azure.

Follow [this guide](/Docs/devguide.md#use-a-proxy-server-to-connect-your-concentrator-to-azure) to:

1. Configure the Docker daemon and the IoT Edge daemon on your device to use a proxy server.
2. Configure the `edgeAgent` properties in the `config.yaml` file on your device.
3. Set environment variables for the IoT Edge runtime in the deployment manifest.
4. Add the `https_proxy` environment variable to the `LoRaWanNetworkSrvModule` in IoT Hub.


## How to provision a LoRA device in Azure IoT Hub

A LoRa device is a normal IoT Hub device with some specific device twin tags. You manage it like you would with any other IoT Hub device.
>To avoid caching issues you should not allow the device to join or send data before it is provisioned in IoT Hub. In case that you did, please follow the ClearCache procedure [here](#Cache-Clearing).

### ABP (personalization) and OTAA (over the air) provisioning

- Login in to the Azure portal; Go to IoT Hub -> IoT devices -> Add
- Use the DeviceEUI as DeviceID -> Save
- Click on the newly created device
- Click on Device Twin menu

- Add the following desired properties for OTAA:

```json
"desired": {
    "AppEUI": "App EUI",
    "AppKey": "App Key",
    "GatewayID": "",
    "SensorDecoder": ""
  },
```

Or the followings desired properties for ABP:

> DevAddr must be unique for every device. It is like an IP Address for LoRA.

```json
"desired": {
    "AppSKey": "Device AppSKey",
    "NwkSKey": "Device NwkSKey",
    "DevAddr": "Device Addr",
    "SensorDecoder": "",
    "GatewayID": ""
  },
```

It should look something like this for ABP:

```json
{
  "deviceId": "BE7A00000000888F",
  "etag": "AAAAAAAAAAs=",
  "deviceEtag": "NzMzMTE3MTAz",
  "status": "enabled",
  "statusUpdateTime": "0001-01-01T00:00:00",
  "connectionState": "Disconnected",
  "lastActivityTime": "2018-08-06T15:16:32.0658492",
  "cloudToDeviceMessageCount": 0,
  "authenticationType": "sas",
  "x509Thumbprint": {
    "primaryThumbprint": null,
    "secondaryThumbprint": null
  },
  "version": 324,
  "tags": {
  
  },
  "properties": {
    "desired": {
      "AppSKey": "2B7E151628AED2A6ABF7158809CF4F3C",
      "NwkSKey": "1B6E151628AED2A6ABF7158809CF4F2C",
      "DevAddr": "0028B9B9",
      "SensorDecoder": "",
      "GatewayID": "",
      "$metadata": {
        "$lastUpdated": "2018-03-28T06:12:46.1007943Z"
      },
      "$version": 1
    },
    "reported": {
      "$metadata": {
        "$lastUpdated": "2018-08-06T15:16:32.2689851Z"
      },
      "$version": 313
    }
  }
}
```

- Click Save
- Turn on the device and you are ready to go
  
## How to customize the LoRa device to Azure communication

The solution supports optional configuration settings that can be configured to enable or disable specific features. These configurations are supported through the Azure IoT Hub Device Twin properties. The following configurations are available:

|Name|Description|Configuration|When to use|
|-|-|-|-|
|Enable/disable downstream messages|Allows disabling the downstream (cloud to device) for a device. By default downstream messages are enabled| Add twin desired property `"Downlink": false` to disable downstream messages. The absence of the twin property or setting value to `true` will enable downlink messages.|Disabling downlink on devices decreases message processing latency, since the network server will not look for cloud to device messages when an uplink is received. Only disable it in devices that are not expecting messages from cloud. Acknowledgement of confirmed upstream are sent to devices even when downlink is set to false|
|Preferred receive window|Allows setting the device preferred receive window (RX1 or RX2). The default preferred receive window is 1| Add twin desired property `"PreferredWindow": 2` sets RX2 as preferred window. The absence of the twin property or setting the value to `1` will set RX1 as preferred window.|Using the second receive window increases the chances that the end application can process the upstream message and send a cloud to device message to the LoRA device without requiring and additional upstream message. Basically completing the round trip in less than 2 seconds.|

> Changes made to twin desired properties in devices that are already connected will only take effect once the network server is restarted or [cache is cleared](#cache-clearing).

## How to decode an incoming packet on IoT Edge

The SensorDecoder tag is used to define which method will be used to decode the LoRA payload. If you leave it out or empty, it will send the raw decrypted payload in the data field of the json message as Base64 encoded value to IoT Hub.

If you want to decode it on the Edge you have the following two options:

1. Specify a method that implements the right logic in the `LoraDecoders` class in the `LoraDecoders.cs` file of the `LoRaWan.NetworkServer`.

2. Adapt the [DecoderSample](./Samples/DecoderSample) which allows you to create and run your own LoRa message decoder in an independent container running on your LoRa gateway without having to edit the main LoRa Engine. [This description](./Samples/DecoderSample#azure-iot-edge-lorawan-starter-kit) shows you how to get started.

In both cases, we have already provided a simple decoder called `"DecoderValueSensor"` that takes the whole payload as a single numeric value and constructs the following json output as a response (The example of an Arduino sending a sensor value as string (i.e. "23.5") is available in the [Arduino folder](./Arduino)):

```json
{
  .....
    "data": {"value": 23.5}
  .....
}
```

To add the sample `"DecoderValueSensor"` to the sample LoRa device configured above, change it's desired properties in IoT Hub as follows for option 1:

```json
"desired": {
    "AppEUI": "App EUI",
    "AppKey": "App Key",
    "GatewayID": "",
    "SensorDecoder": "DecoderValueSensor"
  },
```

or as follows for option 2:

```json
"desired": {
    "AppEUI": "App EUI",
    "AppKey": "App Key",
    "GatewayID": "",
    "SensorDecoder": "http://your_container_name/api/DecoderValueSensor"
  },
```

The `"DecoderValueSensor"` decoder is not a best practice but it makes it easier to experiment sending sensor readings to IoT Hub without having to change any code.

If the SensorDecoder tag has a "http" in it's string value, it will forward the decoding call to an external decoder, as described in option 2 above, using standard Http. The call expects a return value with the same format as the JSON above or an error string.

## How to clear cache

Due to the gateway caching the device information (tags) for 1 day, if the device tries to connect before you have provisioned it, it will not be able to connect because it will be considered a device for another LoRa network.
To clear the cache and allow the device to connect, follow these steps:

- IoT Hub -> IoT Edge -> click on the device ID of your gateway
- Click on LoRaWanNetworkSrvModule
- Click Direct Method
- Type "ClearCache" on Method Name
- Click Invoke Method

Alternatively you can restart the Gateway or the *LoRaWanNetworkSrvModule* container.

## How to configure logging levels for the Network Server

There is a logging mechanism that outputs valuable information to the console of the docker container and can optionally forward these messages to IoT Hub.

You can control logging with the following environment variables in the **LoRaWanNetworkSrvModule** IoT Edge module:

| Variable  | Value                | Explanation                                                                              |
|-----------|----------------------|------------------------------------------------------------------------------------------|
| LOG_LEVEL | "1" or "Debug"       | Everything is logged, including the up- and downstream messages to the packet forwarder. |
|           | "2" or "Information" | Errors and information are logged.                                                       |
|           | "3" or "Error"       | Only errors are logged. (default if omitted)                                             |

For production environments, the **LOG_LEVEL** should be set to **Error**.

Setting **LOG_LEVEL** to **Debug** causes a lot of messages to be generated. Make sure to set **LOG_TO_HUB** to **false** in this case.

| Variable   | Value | Explanation                                          |
|------------|-------|------------------------------------------------------|
| LOG_TO_HUB | true  | Log info are sent from the module to IoT Hub.        |
|            | false | Log info is not sent to IoT Hub (default if omitted) |

You can use VSCode, [IoTHub explorer](https://github.com/Azure/iothub-explorer) or [Device Explorer](https://github.com/Azure/azure-iot-sdk-csharp/tree/master/tools/DeviceExplorer) to monitor the log messages directly in IoT Hub if **LOG_TO_HUB** is set to **true**.

Log in to the gateway and use `sudo docker logs LoRaWanNetworkSrvModule -f` to follow the logs if you are not logging to IoT Hub.

| Variable       | Value | Explanation                             |
|----------------|-------|-----------------------------------------|
| LOG_TO_CONSOLE | true  | Log to docker logs (default if omitted) |
|                | false | Does not log to docker logs             |

## How to configure cloud to device messaging

The solution supports sending Cloud to device (C2D) messages to LoRa messages using [standard IoT Hub Sdks](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-c2d). Cloud to device messages require a Fport message property being set or it will be refused (as shown in the figure below from the Azure Portal). 

![C2D portal](/Docs/Pictures/cloudtodevice.png)

The following tools can be used to send cloud to devices messages from Azure :

* [Azure Portal](http://portal.azure.com) -> IoT Hub -> Devices -> message to device
* [Device Explorer](https://github.com/Azure/azure-iot-sdk-csharp/tree/master/tools/DeviceExplorer)
* [Visual Studio Code IoT Hub Extension](https://marketplace.visualstudio.com/items?itemName=vsciot-vscode.azure-iot-toolkit) 

It is possible to add a 'Confirmed' message property set to true,in order to send the C2D message as ConfirmedDataDown to the LoRa device (as in picture above and below). You can enable additional message tracking options by setting the C2D message id to a value (C2D message ID is automatically populated with the Device Explorer tool used in the image below). 

![C2D portal](/Docs/Pictures/sendC2DConfirmed.png)

As soon as the device acknowledges the message, it will report it in the logs and as a message property named 'C2DMsgConfirmed' on a message upstream (or generate an empty message in case of an empty ack message). The value of the message property will be set to the C2D message id that triggered the response if not null, otherwise to 'C2D Msg Confirmation'. You can find here below a set of picture illustrating the response when the C2D message id was sent to the value '4d3d0cd3-603a-4e00-a441-74aa55f53401'.



![C2D portal](/Docs/Pictures/receiveC2DConfirmation.png)