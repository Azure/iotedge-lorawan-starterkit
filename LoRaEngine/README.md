# LoRaEngine

A **.NET Core 2.1** solution with the following projects:

- **modules** - Azure IoT Edge modules.
  - **LoRaWanPktFwdModule** packages the network forwarder into an IoT Edge compatible docker container. See https://github.com/Lora-net/packet_forwarder and https://github.com/Lora-net/lora_gateway.
  - **LoRaWanNetworkSrvModule** - is the LoRaWAN network server implementation.
- **LoraKeysManagerFacade** - An Azure function handling device provisioning (e.g. LoRa network join, OTAA) with Azure IoT Hub as persistence layer.
- **LoRaDevTools** - library for dev tools (git submodule)

## The overall architecture

This schema represent the various components and how they intereact to have a better understand of the various solution elements.

![schema](/pictures/detailedschema.png)

1. Once the IoT Edge engine start on the Edge device, the code modules are downloaded from the Azure Container Registry.
2. The module containing the ```LoRaWan network server``` is downloaded on the Edge device

Notes:

- The Edge device can be on the same machine at the LoRaWan gateway but not necessary
- The LoRaWan gateway must implement a UDP server on port 1680 to forward the LoRa commands from/to the ```LoRaWan Network Server``` module. In our case it is called ```LoRaWan Packet Forwarder```

3. The LoRaWan Network Server request status for the LoRa devices. The Azure Function ```LoraKeysManagerFacade``` is used as permanent storage for the applicaiton device keys of the devices so nothing is permanently stored into the container.
4. In the case you're using the demo version with the automatic deployment Azure Resource Manager (ARM) template: the Azure function ```LoraKeysManagerFacade``` will register the device ```47AAC86800430028``` into the Azure IoT Hub. Otherwise it does not.
5. The Azure function ```LoraKeysManagerFacade``` sends back the device details to the module
6. The ```LoRaWan Network Server``` module:

- register the device on the LoRa Gateway if needed
- gather the LoRa sensor data from the LoRaWan gateway thru the ```LoRaWan Packet Forwarder```
- decode the LoRa data if requested

7. Publish the LoRa sensor data to Azure IoT Hub

Another view of the architecture and a more message driven view is the following:

![architecture details](/pictures/archidetails2.jpg)

## Getting started with: Build and deploy LoRaEngine

The following guide describes the necessary steps to build and deploy the LoRaEngine to an [Azure IoT Edge](https://azure.microsoft.com/en-us/services/iot-edge/) installation on a LoRaWAN antenna gateway.

### Used Azure services

- [Azure IoT Hub](https://azure.microsoft.com/en-us/services/iot-hub/)
- [Azure Container registry](https://azure.microsoft.com/en-us/services/container-registry/)
- [Azure Functions](https://azure.microsoft.com/en-us/services/functions/)
- [Redis Cache](https://azure.microsoft.com/en-us/services/cache/)

### Prerequisites

- Have LoRaWAN concentrator and edge node hardware ready for testing. The LoRaEngine has been tested and build for various hardware setups. However, for this guide we used the [Seeed LoRa/LoRaWAN Gateway Kit](http://wiki.seeedstudio.com/LoRa_LoRaWan_Gateway_Kit/) and concentrator and the [Seeeduino LoRaWAN](http://wiki.seeedstudio.com/Seeeduino_LoRAWAN/) as edge node.
- [Installed Azure IoT Edge](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-linux-arm) on your LoRaWAN concentrator enabled edge device.
- SetUp an Azure IoT Hub instance and be familiar with [Azure IoT Edge module deployment](https://docs.microsoft.com/en-us/azure/iot-edge/quickstart-linux) mechanism.
- Be familiar with [Azure IoT Edge module development](https://docs.microsoft.com/en-us/azure/iot-edge/quickstart-linux). Note: the following guide expects that your modules will be pushed to [Azure Container registry](https://azure.microsoft.com/en-us/services/container-registry/).
- Create a new IoT Edge device in you IoT Hub with a name of your choice and the default settings.

### Create Redis Cache

- Create a `Redis Cache` in your resource group and the region you are using with a `DNS Name` of your choice and of the size `Standard C0`. Leave all other settings unchanged.
- Navigate to your Redis Cache and from Settings -> Access Keys, note the `Primary connection string (StackExchange.Redis)`.

### Setup Azure function facade and [Azure Container registry](https://azure.microsoft.com/en-us/services/container-registry/)

- Deploy the [function](LoraKeysManagerFacade). In VSCode with the [functions plugin](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azurefunctions) you can run the command `Azure Functions: Deploy to function app...`. Then you have to select the folder `LoraKeysManagerFacade/bin/Release/netstandard2.0/publish` (unfortunately at time of this writing we saw the behavior that VSCode is proposing the wrong folder) and select for the environment `C#` in version `beta`.

- Configure IoT Hub and Redis connection strings in the function:

Copy your Redis Cache connection string in a connection string names `RedisConnectionString`

Copy your IoT Hub `Connection string` with owner policy applied:

![Copy IoT Hub Connection string](/pictures/CopyIoTHubString.PNG)

Now paste it into `Application settings` -> `Connection strings` as `IoTHubConnectionString` of type `Custom`:

![Paste IoT Hub Connection string](/pictures/FunctionPasteString.PNG)

Also, add the previously saved `Primary connection string (StackExchange.Redis)` from your Redis Cache to the `Connection strings` of your function. Use type `Custom` again.

![Add Redis Cache Connection string](/pictures/FunctionRedisKey.PNG)

From the Facade Azure function, extract the `Host key` of type `_master` and save it somewhere. (We will need it in the next step)

![Extract Facade function Host key](/pictures/FunctionHostKey.PNG)

- Configure your `.env` file with your [Azure Container registry](https://azure.microsoft.com/en-us/services/container-registry/) as well as the Facade access URL and credentials. Those variables will be used by our [Azure IoT Edge solution template](/LoRaEngine/deployment.template.json). You can find an example of this file [here](/LoRaEngine/modules/example.env)

```{bash}
CONTAINER_REGISTRY_ADDRESS=yourregistry.azurecr.io
CONTAINER_REGISTRY_USERNAME=yourlogin
CONTAINER_REGISTRY_PASSWORD=registrypassword
PKT_FWD_VERSION=0.0.3
NET_SRV_VERSION=0.0.2
REGION=EU
FACADE_SERVER_URL=https://yourfunction.azurewebsites.net/api/
FACADE_AUTH_CODE=functionpassword
```

### Use a Proxy server to connect your Concentrator to Azure

**This step is optional and should only be executed if your concentrator needs to use a proxy server to communicate with Azure**

Follow the guide on [configuring an IoT Edge device to communicate through a proxy server](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-configure-proxy-support) to:

1. Configure the Docker daemon and the IoT Edge daemon on your device to use a proxy server.
2. Configure the edgeAgent properties in the config.yaml file on your device.
3. Set environment variables for the IoT Edge runtime in the deployment manifest. 

After that, add the environment variable ```https_proxy``` to the ```LoRaWanNetworkSrvModule``` in your ```IoT Hub``` -> ```IoT Edge``` -> ```Edge Device``` -> ```Set Modules``` section.

![IoT Hub Edge Device Module Setting](/pictures/EdgeSetProxy.PNG)

**End of optional proxy configuration**

### Setup concentrator with Azure IoT Edge

- Note: if your LoRa chip set is connected by SPI on raspberry PI bus don't forget to [enable it](https://www.makeuseof.com/tag/enable-spi-i2c-raspberry-pi/), (You need to restart your pi).

- Build and deploy Azure IoT Edge solution

We will use [Azure IoT Edge for Visual Studio Code](https://marketplace.visualstudio.com/items?itemName=vsciot-vscode.azure-iot-edge) extension to build, push and deploy our solution.

Make sure you are logged in to the Azure Container Registry you are using. Run `docker login <mycontainerregistry>.azurecr.io` on your development machine.

Now, build an push the solution by right clicking [deployment.template.json](/LoRaEngine/deployment.template.json) and select `Build and Push IoT Edge Solution` (look as alternative into [deployment.template.amd64.json](/LoRaEngine/deployment.template.amd64.json) for x64 based gateways)

![VSCode: Build and push edge solution](/pictures/CreateEdgeSolution.PNG)

After that you can push the solution to your IoT Edge device by right clicking on the device and select `Create Deployment for single device`

![VSCode: Deploy edge solution](/pictures/DeployEdge.PNG)

### Provision LoRa leaf device

The [sample code](/Arduino/TemperatureOtaaLora/TemperatureOtaaLora.ino) used in this example is based on [Seeeduino LoRaWAN](http://wiki.seeedstudio.com/Seeeduino_LoRAWAN/) with a [Grove - Temperature Sensor](http://wiki.seeedstudio.com/Grove-Temperature_Sensor_V1.2/). It sends every 30 seconds its current temperature reading and prints out a Cloud-2-Device message if one is transmitted in its receive window.

The sample has configured the following sample [device identifiers and credentials](https://www.thethingsnetwork.org/docs/lorawan/security.html):

- DevEUI: `47AAC86800430010`
- AppEUI: `BE7A0000000014E3`
- AppKey: `8AFE71A145B253E49C3031AD068277A3`

You will need your own identifiers when provisioning the device.

Look out for these code lines:

```arduino
lora.setId(NULL, "47AAC86800430010", "BE7A0000000014E3");
lora.setKey(NULL, NULL, "8AFE71A145B253E49C3031AD068277A3");
```

To provisioning a device in Azure IoT Hub with these identifiers and capable to [decode](/LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWan.NetworkServer/LoraDecoders.cs) temperature payload into Json you have to create a device with:

Device Id: `47AAC86800430010` and Device Twin's deired properties:

```json
"desired": {
  "AppEUI": "BE7A0000000014E3",
  "AppKey": "8AFE71A145B253E49C3031AD068277A3",
  "SensorDecoder": "DecoderValueSensor"
}
```

![Create device in Azure IoT Hub](/pictures/CreateDevice.PNG)

![Set device twin in Azure IoT Hub](/pictures/DeviceTwin.PNG)

### Device to Cloud and Cloud to Device messaging in action

As soon as you start your device you should see the following:

- [DevAddr, AppSKey and NwkSKey](https://www.thethingsnetwork.org/docs/lorawan/security.html) are generated and stored in the Device Twin, e.g.:

```json
"desired": {
    "AppEUI": "BE7A0000000014E3",
    "AppKey": "8AFE71A145B253E49C3031AD068277A3",
    "SensorDecoder": "DecoderTemperatureSensor",
    "AppSKey": "5E8513F64D99A63753A5F0DBB9FB9F91",
    "NwkSKey": "C0EF4B9495BD4A4C32B42438CD52D4B8",
    "DevAddr": "025DEAAE",
    "DevNonce": "D9B6"
  }
```

- If you follow the logs of the network server module (e.g. `sudo iotedge logs LoRaWanNetworkSrvModule -f`) you can follow the LoRa device join:

```text
{"rxpk":[{"tmst":3831950603,"chan":2,"rfch":1,"freq":868.500000,"stat":1,"modu":"LORA","datr":"SF7BW125","codr":"4/5","lsnr":8.5,"rssi":-30,"size":23,"data":"AOMUAAAAAHq+EABDAGjIqkfZtkroyCc="}]}
Join Request Received
{"txpk":{"imme":false,"data":"IE633dznxvgA89ZTkH1jET0=","tmst":3836950603,"size":17,"freq":868.5,"rfch":0,"modu":"LORA","datr":"SF7BW125","codr":"4/5","powe":14,"ipol":true}}
Using edgeHub as local queue
Updating twins...
Join Accept sent
TX ACK RECEIVED
```

- Every 30 seconds the temperature is transmitted by the device, e.g.:

```json
{
  "time": null,
  "tmms": 0,
  "tmst": 4226472308,
  "freq": 868.5,
  "chan": 2,
  "rfch": 1,
  "stat": 1,
  "modu": "LORA",
  "datr": "SF12BW125",
  "codr": "4/5",
  "rssi": -33,
  "lsnr": 7.5,
  "size": 18,
  "data": {
    "temperature": 18.78
  },
  "EUI": "47AAC86800430010",
  "gatewayId": "berry2",
  "edgets": 1534253192857
}
```

Note: an easy way to follow messages send from the device is again with VSCode: right click on the device in the explorer -> `Start Monitoring D2C Message`.

This is how a complete transmission looks like:

![Complete transmission](/pictures/RoundtripTemp.PNG)

You can even test sending Cloud-2-Device message (e.g. by VSCode right click on the device in the explorer -> `Send C2D Message To Device`).

The Arduino example provided above will print the message on the console. Keep in mind that a [LoRaWAN Class A](https://www.thethingsnetwork.org/docs/lorawan/) device will only receive after a transmit, in our case every 30 seconds.

## Debugging outside of IoT Edge and docker

It is possible to run the bits in the LoRaEngine locally with from Visual Studio in order to enable a better debugging experience. Here are the steps you will need to enable this feature:

1. Change the value *server_adress* in the file *local_conf.json* (located in LoRaEngine/modules/LoRaWanPktFwdModule) to point to your computer. Rebuild and redeploy the container.
2. If you are using a Wireless and Windows, make sure your current Wireless network is set as Private in your Windows settings. Otherwise you won't receive the UDP packets.
3. Open the properties of the project *LoRaWanNetworkServerModule* and set the following values under the Debug tab:
  - IOTEDGE_IOTHUBHOSTNAME : XXX.azure-devices.net (XXX = your iot hub hostname)
  - ENABLE_GATEWAY : false
  - LOG_LEVEL : 1 (optional, to activate most verbose logging level)
  - FacadeServerUrl : http://localhost:7071/api/ (or pointer to the function you want to use)
  - FacadeAuthCode : <your function auth code, do not set this variable if running locally>
4. Add a local.settings.json in the project LoRa KeysManagerFacade with
```json
     {
  "IsEncrypted": false,
  "values": {
    "AzureWebJobsStorage": "<Connection String of your deployed blob storage>",
    "WEBSITE_CONTENTSHARE": "<Name of your Azure function>"

  },
  "ConnectionStrings": {
    "IoTHubConnectionString": "<Connection string of your IoT Hub Owner (go to keys -> IoT Hub owner and select the connection string)>",
    "RedisConnectionString": "<your Redis connection string>"
  }

}
 ```
5. Right click on your solution and select properties, select multiple startup projects. Start LoRaWanNetworkSrvModule and LoRaKeysManagerFacade.

6. If you hit start in your VS solution, you will receive messages directly from your packet forwarder. You will be able to debug directly from your computer. 

Happy Debugging! 


