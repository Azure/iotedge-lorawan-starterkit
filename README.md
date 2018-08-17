# Azure IoT Edge LoRaWAN Starter Kit

Experimental sample implementation of LoRaWAN components to connect LoRaWAN antenna gateway running IoT Edge directly with Azure IoT.

The goal of the project is to provide guidance and a reference for Azure IoT Edge users to experiment with LoRaWAN technology.

## Background

LoRaWAN is a type of wireless wide-area networking that is designed to allow long-range communication at a low bit rate among low-power connected objects, such as sensors operated on a battery.

Network topology is of star-of-stars type, with the leaf sensors sending data to gateways for forwarding telemetry to and receiving commands from backing Internet services. Nowadays, even for simple scenarios like having 10 devices connected to a single LoRaWan gateway (hardware with antenna), you need to connect your gateway to a Network Server and then work through connectors provided by the server vendor to integrate your LoRa gateways and devices with the back end. These setups can be connected to Azure IoT Hub quite easily. As a matter of fact [such scenarios exist](https://github.com/loriot/AzureSolutionTemplate). Customers looking for an operated network with national or international reach (e.g. fleet operators, logistics) will tend to choose this setup accepting the potentially higher complexity and dependency on the network operator.

However, customers looking for any of the following are expected to prefer a setup where the LoRaWAN network servers runs directly on the gateway/Azure IoT Edge:

- Primarily coverage on their own ground (e.g. manufacturing plants, smart buildings, facilities, ports).
- Capabilities that Azure IoT edge brings to the table:
  - Local processing on the gateway.
  - Offline capabilities of the gateway.
  - Gateway management.
- Homogenous management of devices and gateways independent of connectivity technology.

## Functionality

- Support of Class A devices
- Activation through ABP and OTAA
- Confirmed and unconfirmed upstream messages
- Confirmed downstream messages
- Device and Gateway management done completely in Azure IoT Hub

## Current limitations

- Multigateway works but is not fully tested and you need to implement message deduplication after IoT Hub, if multiples gateways are used in the same range of the device we recommend setting the gateway tag "GatewayID" on the device twins with the IoT Edge ID of the preferred gateway for that device.
- No Class B and C
- No ADR
- No Mac commands
- Tested only for EU frequency
- Max 51 bytes downstream payload, longer will be cut. It supports multiple messages with the fpending flag
- IoT Edge must have internet connectivity, it can work for limited time offline if the device has previously transmitted an upstream message.
- The [network server Azure IoT Edge module](/LoRaEngine/modules/LoRaWanNetworkSrvModule) and the [Facade function](/LoRaEngine/LoraKeysManagerFacade) have an API dependency on each other. its generally recommended for the deployments on the same source level.

- In addition we generally recommend as read the [Azure IoT Edge trouble shooting guide](https://docs.microsoft.com/en-us/azure/iot-edge/troubleshoot)

## Tested Gateway HW

- [Seeed Studio LoRa LoRaWAN Gateway - 868MHz Kit with Raspberry Pi 3](https://www.seeedstudio.com/LoRa-LoRaWAN-Gateway-868MHz-Kit-with-Raspberry-Pi-3-p-2823.html)

- [AAEON AIOT-ILRA01 LoRa® Certified Intel® Based Gateway and Network Server](https://www.aaeon.com/en/p/intel-lora-gateway-system-server)

## Architecture

![Architecture](pictures/EdgeArchitecture.png)

## Directory Structure

The code is organized into three sections:

- **LoRaEngine** - a .NET Standard 2.0 solution with the following folders:
  - **modules** - Azure IoT Edge modules.
  - **LoraKeysManagerFacade** - An Azure function handling device provisioning (e.g. LoRa network join, OTAA) with Azure IoT Hub as persistence layer.
  - **LoRaDevTools** - library for dev tools (git submodule)
- **Arduino** - Examples and references for LoRa Arduino based devices.
- **Template** - Contain code useful for the "deploy to Azure button"

## Reporting Security Issues

Security issues and bugs should be reported privately, via email, to the Microsoft Security
Response Center (MSRC) at [secure@microsoft.com](mailto:secure@microsoft.com). You should
receive a response within 24 hours. If for some reason you do not, please follow up via
email to ensure we received your original message. Further information, including the
[MSRC PGP](https://technet.microsoft.com/en-us/security/dn606155) key, can be found in
the [Security TechCenter](https://technet.microsoft.com/en-us/security/default).

## Quick start

An Azure deployment template is available to deploy all the required Azure infrastructure and get you started quickly. 
If you'd rather deploy it manually please jump directly into the [do it yourself section](/LoRaEngine).

### Prequisites & Limitations
Currently, the template work only with ARM based gateways, like a Raspberry Pi, support for x86 will be added in a future release. (you could actually already deploy it for intel by following the instructions in the [do it yourself section](/LoraEngine))
The template was tested to work on the following gateway types:
* [Seeed Studio LoRa LoRaWAN Gateway - 868MHz Kit with Raspberry Pi 3](https://www.seeedstudio.com/LoRa-LoRaWAN-Gateway-868MHz-Kit-with-Raspberry-Pi-3-p-2823.html)

The LoRa device demo code for Arduino is built only for Seeduino LoRaWan board and was not test with other Arduino LoRa boards.

### Deployed Azure Infrastructure
The template will deploy in your Azure subscription the Following ressources:
* [IoT Hub](https://azure.microsoft.com/en-us/services/iot-hub/)
* [Azure Function](https://azure.microsoft.com/en-us/services/functions/)

### Step-by-step instructions
1. Press on the button here below to start your Azure Deployment.

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FSkraelinger%2FAzureIoT_LoRaWan_StarterKit%2Fmaster%2FTemplate%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

2. You will get to a page asking you to fill the following fields :

- **Resource Group** - A logical "folder" where all the template resource would be put into, just choose a meaningful name.
- **Location** - In which Datacenter the resources should be deployed.
- **Unique Solution Prefix** - A string that would be used as prefix for all the resources name to ensure their uniqueness. Hence, avoid any standard prefix such as "lora" as it might already be in use and might make your deployment fail.
- **Edge gateway name** - the name of your LoRa Gateway node in the IoT Hub.
- **Deploy Device** - Do you want a demo end device to be already provisioned. If yes the code located in the [Arduino folder](/Arduino) would be ready to use immediately.

  The deployment would take c.a. 10 minutes to complete.

3.  During this time, you can proceed to [install IoT Edge to your gateway](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-linux-arm). 

4. Once the Azure deployment is finished, connect your IoT Edge with the cloud [as described in point 3](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-linux-arm#configure-the-azure-iot-edge-security-daemon). You can get the connection string by clicking on the deployed IoT Hub -> IoT Edge Devices -> Connection string, as shown in the picture below.

5. If your gateway is a Raspberry Pi, **don't forget to [enable SPI](https://www.makeuseof.com/tag/enable-spi-i2c-raspberry-pi/) , (You need to restart your pi)**.

By using the `docker ps` command, you should see the Edge containers being deployed on your local gateway. You can now try one of the samples in the [Arduino folder](/Arduino) to see LoRa messages being sent to the cloud.

### What does the template do?
The template provision an IoT Hub with a [packet forwarder](https://github.com/Lora-net/packet_forwarder) and a network server module already preconfigured to work out of the box. As soon as you connect your IoT Edge device in point 4 above, those will be pushed on your device. You can find template definition and Edge deployment specification [here](/Template).

## Customize the solution & Deep dive

Have a look at the [LoRaEngine folder](/LoRaEngine) for more in details explanation.

## License

This repository is licensed with the [MIT](LICENSE) license.
