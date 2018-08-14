# LoRaEngine

A **.NET Standard 2.0** solution with the following projects:

- **PacketForwarderHost** - executable and IoTEdge configuration files.
- **UDPListener** - executable
- **LoRaTools** - library
- **LoRaServer** - IoT edge module executable, Dockerfile, etc.
- **DevTools** - submodule folder, check it out using the command
  `git submodule update --init --recursive` and add the projects to the visual studio solution
  - **PacketForwarderSimulator** - executable
  - **DevTool1** - executable used during development process
  - **DevTool2** - executable used during development process
  - etc . . .

**NOTE:** Until we have a unit test framework in place, we are relying on small executables, maintained by each developer, for the purposes of testing and demonstrating module functionality. Each of these executables should have a README.md file that demonstrates how to run the code.

## Getting started with: Build and deploy LoRaEngine

The following guide describes the necessary steps to build and deploy the LoRaEngine to an [Azure IoT Edge](https://azure.microsoft.com/en-us/services/iot-edge/) installation.

### Used Azure services

- [Azure IoT Hub](https://azure.microsoft.com/en-us/services/iot-hub/)
- [Azure Container registry](https://azure.microsoft.com/en-us/services/container-registry/)
- [Azure functions](https://azure.microsoft.com/en-us/services/functions/)

### Prerequisites

- Have LoRaWAN concentrator and edge node hardware ready for testing. The LoRaEngine has been tested and build for various hardware setups. However, for this guide we used the [Seeed LoRa/LoRaWAN Gateway Kit](http://wiki.seeedstudio.com/LoRa_LoRaWan_Gateway_Kit/) and concentrator and the [Seeeduino LoRaWAN](http://wiki.seeedstudio.com/Seeeduino_LoRAWAN/) as edge node.
- [Installed Azure IoT Edge](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-linux-arm) on your LoRaWAN concentrator enabled edge device.
- SetUp an Azure IoT Hub instance and be familiar with [Azure IoT Edge module deployment](https://docs.microsoft.com/en-us/azure/iot-edge/quickstart-linux) mechanism.
- Be familiar with [Azure IoT Edge module development](https://docs.microsoft.com/en-us/azure/iot-edge/quickstart-linux). Note: the following guide expects that your modules will be pushed to [Azure Container registry](https://azure.microsoft.com/en-us/services/container-registry/).

### SetUp concentrator with Azure IoT Edge

- Note: if your LoRa chip set is connected by SPI bus please ensure that it is enabled, e.g. on [Raspberry Pi](https://www.raspberrypi.org/documentation/hardware/raspberrypi/spi/README.md).
- Configure your `.env` file with your [Azure Container registry](https://azure.microsoft.com/en-us/services/container-registry/) access URL and credentials, e.g.:

```{bash}
CONTAINER_REGISTRY_USERNAME=myregistryrocks
CONTAINER_REGISTRY_PASSWORD=ghjGD5jrK6667
CONTAINER_REGISTRY_ADDRESS=myregistryrocks.azurecr.io
FACADE_SERVER_URL=https://lorafacadefunctionrocks.azurewebsites.net/api/
FACADE_AUTH_CODE=gkjhFGHFGGjhhg5645674==
```

- Build network packet forwarder

Our LoraPktFwdFiltermodule packages the into an IoT Edge compatible docker container. However, the actualy binary is not part of our gihub repository. If the forwarder is not shipped with your device you can as well compile it on your own. You will need the following repositories for that: https://github.com/Lora-net/packet_forwarder and https://github.com/Lora-net/lora_gateway.

The `lora_pkt_fwd` binary has to be copied `LoraPktFwdFiltermodule`directory.

- Build and deploy entire solution (VsCode)

### SetUp Azure function facade:

- deploy them
- configure IoT Hub access key
- configure properties on module (facade access)

### Provision LoRa device

- create device with LoRa key
- Set tags for app ID and key
