# Quick Start

An Azure deployment template is available to deploy all the required Azure infrastructure and get you started quickly.
If you'd rather deploy it manually please jump directly into the [do it yourself section](devguide.md).

### Prerequisites
The template supports x86 and ARM architectures and will automatically deploy the correct version to your gateway. Make sure to provide your gateway's reset pin in the dialog before the deployment.

Additionally, if your gateway use SPI_DEV version 1 the packet forwarder module will not work out-of-the-box. To fix this, simply add an environment variable 'SPI_DEV' set to the value 1 to the LoRaWanPktFwdModule module (SPI_DEV is set to 2 by default).

The LoRa device demo code in the Arduino folder is built only for Seeduino LoRaWan board and was not test with other Arduino LoRa boards.

### Deployed Azure Infrastructure

The template will deploy in your Azure subscription the Following resources:

- [IoT Hub](https://azure.microsoft.com/en-us/services/iot-hub/)
- [Azure Function](https://azure.microsoft.com/en-us/services/functions/)
- [Redis Cache](https://azure.microsoft.com/en-us/services/cache/)

### Step-by-step instructions

1. Press on the button here below to start your Azure Deployment.

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fiotedge-lorawan-starterkit%2Fmaster%2FTemplate%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

2. You will get to a page asking you to fill the following fields :

- **Resource Group** - A logical "folder" where all the template resource would be put into, just choose a meaningful name.
- **Location** - In which DataCenter the resources should be deployed.
- **Unique Solution Prefix** - A string that would be used as prefix for all the resources name to ensure their uniqueness. Hence, avoid any standard prefix such as "lora" as it might already be in use and might make your deployment fail.
- **Edge gateway name** - the name of your LoRa Gateway node in the IoT Hub.
- **Deploy Device** - Do you want demo end devices to be already provisioned (one using OTAA and one using ABP). If yes, the code located in the [Arduino folder](/Arduino) would be ready to use immediately.
- **Reset pin** - The reset pin of your gateway (the value should be 7 for the Seed Studio LoRaWam, 25 for the IC880A)
- **Region** - In what region are you operating your device (currently only EU868 and US915 is supported)

  The deployment would take c.a. 10 minutes to complete.

3.  During this time, you can proceed to [install IoT Edge to your gateway](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-linux-arm).

4.  Once the Azure deployment is finished, connect your IoT Edge with the cloud [as described in point 3](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-linux-arm#configure-the-azure-iot-edge-security-daemon). You can get the connection string by clicking on the deployed IoT Hub -> IoT Edge Devices -> Connection string, as shown in the picture below.

5.  If your gateway is a Raspberry Pi, **don't forget to [enable SPI](https://www.makeuseof.com/tag/enable-spi-i2c-raspberry-pi/) , (You need to restart your pi)**.

By using the `docker ps` command, you should see the Edge containers being deployed on your local gateway. You can now try one of the samples in the [Arduino folder](/Arduino) to see LoRa messages being sent to the cloud. If you have checked the Deploy Device checkbox you can use this sample directly "TransmissionTestOTAALoRa.ino" without provisioning the device first.

### What does the template do?

The template provision an IoT Hub with a [packet forwarder](https://github.com/Lora-net/packet_forwarder) and a network server module already pre-configured to work out of the box. As soon as you connect your IoT Edge device in point 4 above, those will be pushed on your device. You can find template definition and Edge deployment specification [here](/Template).

If you are using the the RAK833-USB, you'll need to adjust the template to use the right LoRaWan Packet Forwarder. You will find a full documentation in this [submodule](/Docs/LoRaWanPktFwdRAK833USB).

## Using a Proxy Server to connect your Concentrator to Azure

This is an optional configuration that should only be executed if your concentrator needs to use a proxy server to communicate with Azure.

Follow [this guide](./LoRaEngine#use-a-proxy-server-to-connect-your-concentrator-to-azure) to:

1. Configure the Docker daemon and the IoT Edge daemon on your device to use a proxy server.
2. Configure the `edgeAgent` properties in the `config.yaml` file on your device.
3. Set environment variables for the IoT Edge runtime in the deployment manifest.
4. Add the `https_proxy` environment variable to the `LoRaWanNetworkSrvModule` in IoT Hub.

## LoRa Device provisioning

A LoRa device is a normal IoT Hub device with some specific device twin tags. You manage it like you would with any other IoT Hub device.
**To avoid caching issues you should not allow the device to join or send data before it is provisioned in IoT Hub. In case that you did plese follow the ClearCache procedure that you find below.**

### ABP (personalization) and OTAA (over the air) provisioning

- Login in to the Azure portal go to IoT Hub -> IoT devices -> Add
- Use the DeviceEUI as DeviceID -> Save
- Click on the newly created device
- Click on Device Twin menu

- Add the followings desired properties for OTAA:

```json
"desired": {
    "AppEUI": "App EUI",
    "AppKey": "App Key",
    "GatewayID": "",
    "SensorDecoder": ""
  },
```

Or the followings desired properties for ABP:

**DevAddr must be unique for every device! It is like an ip address for lora.**

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

### Optional device properties

Customization's to lora devices are set by creating specific twin desired properties on the device. The following customization's are available:

|Name|Description|Configuration|When to use|
|-|-|-|-|
|Enable/disable downstream messages|Allows disabling the downstream (cloud to device) for a device. By default downstream messages are enabled| Add twin desired property `"Downlink": false` to disable downstream messages. The absence of the twin property or setting value to `true` will enable downlink messages.|Disabling downlink on devices decreases message processing latency, since the network server will not look for cloud to device messages when an uplink is received. Only disable it in devices that are not expecting messages from cloud. Acknowledgement of confirmed upstream are sent to devices even when downlink is set to false|
|Preferred receive window|Allows setting the device preferred receive window (RX1 or RX2). The default preferred receive window is 1| Add twin desired property `"PreferredWindow": 2` sets RX2 as preferred window. The absence of the twin property or setting the value to `1` will set RX1 as preferred window.|Using the second receive window increases the chances that the end application can process the upstream message and send a cloud to device message to the lora device without requiring and additional upstream message. Basically completing the round trip in less than 2 seconds.|
|Message Deduplication|Allows controlling the handling of duplicate messages received by multiple gateways.The default is None.| Add twin desired property `"Deduplication": "Drop"` to instruct dropping duplicate messages on the gateway, set it to `"Mark"` if you want to mark messages to the IotHub with `"dupmsg": true` in case the message was already processed. Example payload: Device: [47AAC86800430028], Data:[{"time":null,"tmms":0,"tmst":3201165987,"freq":868.3,"chan":1,"rfch":1,"stat":1,"modu":"LORA","datr":"SF7BW125","codr":"4/5","rssi":-61,"lsnr":8.2,"size":14,"data":{"value":1},"port":10,"fcnt":2,"rawdata":"QImRWQIAAgAK8I3rbqc=","eui":"47AAC86800430028","gatewayid":"simulatorpaschule1","edgets":1550501633879,`"dupmsg": true`}]|Deduplication on the gateway allows you to control how you want to handle duplicate messages in a multi gateway environment without having to add additional logic on the receiving or processing end. 

**Important**: changes made to twin desired properties in devices that are already connected will only take effect once the network server is restarted or [cache is cleared](#cache-clearing).

### Decoders

The SensorDecoder tag is used to define which method will be used to decode the LoRa payload. If you leave it out or empty it will send the raw decrypted payload in the data field of the json message as Base64 encoded value to IoT Hub.

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

if the SensorDecoder tag has a "http" in it's string value, it will forward the decoding call to an external decoder, as described in option 2 above, using standard Http. The call expects a return value with the same format as the json here above or an error string.

### Cache Clearing

Due to the gateway caching the device information (tags) for 1 day, if the device tries to connect before you have provisioned it, it will not be able to connect because it will be considered a device for another LoRa network.
To clear the cache and allow the device to connect follow these steps:

- IoT Hub -> IoT Edge -> click on the device ID of your gateway
- Click on LoRaWanNetworkSrvModule
- Click Direct Method
- Type "ClearCache" on Method Name
- Click Invoke Method

Alternatively you can restart the Gateway or the LoRaWanNetworkSrvModule container.

## Monitoring and Logging

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

## Customize the solution & Deep dive

Have a look at the [LoRaEngine folder](/LoRaEngine) for more in details explanation.

## Cloud to device message

The solution support sending Cloud to device (C2D) messages to LoRa messages using [standard IoT Hub Sdks](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-c2d). Cloud to device messages require a Fport message property being set or it will be refused (as shown in the figure below from the Azure Portal). 

![C2D portal](/Docs/Pictures/cloudtodevice.png)

The following tools can be used to send cloud to devices messages from Azure :

* [Azure Portal](http://portal.azure.com) -> IoT Hub -> Devices -> message to device
* [Device Explorer](https://github.com/Azure/azure-iot-sdk-csharp/tree/master/tools/DeviceExplorer)
* [Visual Studio Code IoT Hub Extension](https://marketplace.visualstudio.com/items?itemName=vsciot-vscode.azure-iot-toolkit) 

It is possible to add a 'Confirmed' message property set to true,in order to send the C2D message as ConfirmedDataDown to the LoRa device (as in picture above and below). You can enable additional message tracking options by setting the C2D message id to a value (C2D message ID is automatically populated with the Device Explorer tool used in the image below). 

![C2D portal](/Docs/Pictures/sendC2DConfirmed.png)

As soon as the device acknowledges the message, it will report it in the logs and as a message property named 'C2DMsgConfirmed' on a message upstream (or generate an empty message in case of an empty ack message). The value of the message property will be set to the C2D message id that triggered the response if not null, otherwise to 'C2D Msg Confirmation'. You can find here below a set of picture illustrating the response when the C2D message id was sent to the value '4d3d0cd3-603a-4e00-a441-74aa55f53401'.



![C2D portal](/Docs/Pictures/receiveC2DConfirmation.png)


## MAC Commands

The Solution has an initial support for MAC Commands. Currently only the command Device Status Command is fully testable. The command will return device status (battery and communication margin). To try it, send a Cloud to Device message on your end device and add the following message properties :

```
CidType : 6
```

![MacCommand](/Docs/Pictures/MacCommand.PNG)

