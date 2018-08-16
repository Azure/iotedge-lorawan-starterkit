# Azure IoT LoRaWan Starter Kit

**Project Leads:** [Ronnie Saurenmann](mailto://ronnies@microsoft.com) and
[Todd Holmquist-Sutherland](mailto://toddhs@microsoft.com).

Experimental sample implementation of LoRaWAN components to connect LoRaWAN antenna gateway running IoT Edge directly with Azure IoT.

The goal of the project is to provide guidance and a refernce for Azure IoT Edge users to experiment with LoRaWAN technology.

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

## Tested Gateway HW

- [Seeed Studio LoRa LoRaWAN Gateway - 868MHz Kit with Raspberry Pi 3](https://www.seeedstudio.com/LoRa-LoRaWAN-Gateway-868MHz-Kit-with-Raspberry-Pi-3-p-2823.html)

- [AAEON AIOT-ILRA01 LoRa® Certified Intel® Based Gateway and Network Server](https://www.aaeon.com/en/p/intel-lora-gateway-system-server)

## Architecture

![Architecture](EdgeArchitecture.png)


## Directory Structure

The code is organized into three sections:

- **LoRaEngine** - a .NET Standard 2.0 solution with the following folders:
  - **modules** - Azure IoT Edge modules.
  - **LoraKeysManagerFacade** - An Azure function handling device provisioning (e.g. LoRa network join, OTAA) with Azure IoT Hub as persistence layer.
  - **LoRaDevTools** - library for dev tools (git submodule)
- **Arduino** - Examples and references for LoRa Arduino based devices.
- **EdgeVisualization** - an optional Azure IoT Edge module for visualizing LoRa packet flows inside IoT Edge. Example for local IoT Edge message processing.
- **Template** - Contain code useful for the "deploy to Azure button"

## Quick start

A deployment template is available to deploy all the required Azure infrastructure and get you started quickly. Just press on the "Deploy to Azure button" here below.
If you'd rather deploy it manually please jump directly into the [do it yourself section](/LoRaEngine).

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FSkraelinger%2FAzureIoT_LoRaWan_StarterKit%2Fmaster%2FTemplate%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

You will be ask to fill the following fields :

- **Resource Group** - A logical "folder" where all the template resource would be put into, just choose a meaningful name.
- **Location** - In which Datacenter the resources should be deployed.
- **Unique Solution Prefix** - A string that would be used as prefix for all the resources name to ensure their uniqueness. Hence, avoid any standard prefix such as "lora" as it might already be in use and might make your deployment fail.
- **Edge gateway name** - the name of your LoRa Gateway node in the IoT Hub.
- **Deploy Device** - Do you want a demo end device to be already provisioned. If yes the code located in the [Arduino folder](/Arduino) would be ready to use immediately.

The deployment would take c.a. 10 minutes to complete, in the meanwhile please proceed to [install IoT Edge to your gateway](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-linux-arm).

Once it's done, please head up to the IoT Hub deployed in your template and go to IoT Edge Devices -> [your device name] then copy the connection string.

Now SSH into your gateway, if your gateway is a Raspberry Pi, don't forget to [enable SPI](https://www.makeuseof.com/tag/enable-spi-i2c-raspberry-pi/) , (You need to restart your pi).

By using the `docker ps` command, you should see the Edge containers being deployed on your local gateway. You can now try one of the samples in the [Arduino folder](/Arduino) to see LoRa messages being sent to the cloud.

## Customize the solution & Deep dive

Have a look at the [LoRaEngine folder](/LoRaEngine) for more in details explanation.

## Known constraints

- The [network server Azure IoT Edge module](/LoRaEngine/modules/LoRaWanNetworkSrvModule) and the [Facade function](/LoRaEngine/LoraKeysManagerFacade) have an API dependency on each other. its generally recommended for the deployments on the same source level.
- We generally recommend as read the [Azure IoT Edge trouble shooting guide](https://docs.microsoft.com/en-us/azure/iot-edge/troubleshoot)
  This project is aimed at providing an easy way to connect LoRa sensors/gateways to the Azure Cloud.

## License

This repository is licensed with the [MIT](LICENSE) license.
