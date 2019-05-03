
[![Build Status](https://dev.azure.com/epicstuff/Azure%20IoT%20Edge%20LoRaWAN%20Starter%20Kit/_apis/build/status/CI-MultiGateway?branchName=master)](https://dev.azure.com/epicstuff/Azure%20IoT%20Edge%20LoRaWAN%20Starter%20Kit/_build/latest?definitionId=62&branchName=master)
[![Build Status](https://dev.azure.com/epicstuff/Azure%20IoT%20Edge%20LoRaWAN%20Starter%20Kit/_apis/build/status/CI-MultiGateway?branchName=dev)](https://dev.azure.com/epicstuff/Azure%20IoT%20Edge%20LoRaWAN%20Starter%20Kit/_build/latest?definitionId=62&branchName=dev)

# Azure IoT Edge LoRaWAN Starter Kit

The Azure IoT Edge LoRaWAN starter kit is an *experimental* cross platform LoRaWAN implementation following the [LoRaWAN 1.0.2 specification](https://lora-alliance.org/resource-hub/lorawantm-specification-v102). The goal of the solution is to provide a turnkey experience for users looking to build their own private LoRa network and connect their LoRaWAN devices and gateways to Azure IoT Hub without the need of any intermediaries or third party services. The kit provides a LoRaWAN packet forwarder and network server implementation built on top of [Azure IoT Edge](https://azure.microsoft.com/en-us/services/iot-edge/) infrastructure and all other components required to enable seamless communication to Azure. It significantly reduces the effort of building your own custom network server and deployment to multiple gateways. 
Some of the key benefits include: 

- Deploy your own LoRa private network and connect to Azure within minutes. No intermediaries or third party required. [Get Started Now](#getting-started). 
- Leverage the power of [Azure IoT Edge](https://azure.microsoft.com/en-us/services/iot-edge/) such as Edge processing, dynamic provisioning, remote firmware updates for your LoRaWAN gateways.
- Enterprise ready with features like proxy support, offline support, and complete cloud based management. [See complete list of features](#features).
- Extensible [LoRa Engine](/Docs/devguide.md) and a custom [Decoder framework](/Samples/DecoderSample/ReadMe.md) for specialized implementations.
- Stress tested for **EU868** and **US915** frequencies with real customers and [multiple LoRaWAN gateways](#tested-gateways).
- Supporting tools such as comprehensive unit tests, CI/CD pipeline, stress testing, simulator and a command line interface.
  
![Architecture](/Docs/Pictures/EdgeArchitecture.png)
  ## Table of Contents
  - [Features](#features)
  - [LoRaWAN Specification Support](#loRaWAN-specification-support)
  - [Getting Started](#getting-started)
  - - [Prerequisites](#prerequisites)
  - - [Deploy to Azure](#deploy-to-azure)
  - [Troubleshooting](#troubleshooting)
  - [Tested Gateways](#tested-gateways)
  - [Support](#support)
  - [Contributing](#contributing)

## Features
- Compliant with LoRaWAN 1.0.2 implementation (see [LoRaWAN Specification Supported features](#LoRaWAN--Specification-Support)).
- Device and Gateway management done completely through Azure IoT Hub.
- Bi-directional communication between LoRa end devices and Azure cloud.
- Custom packet decoding framework.
- Identity Translation for LoRa devices with caching support.
- Partial Offline and Casually connected Gateways scenarios.*
- Easy deployment and setup using Azure ARM templates.
- Small to Midsize Scalability Tests.
- Simulator for development and testing without the need to own a Gateway.
- Command Line Interface for common operations.*
**Planned or In Development features*
## LoRaWAN Specification Support 
We plan to support the following key features of LoRaWAN 1.0.2 specification, however please note that not all of them are available as of today. Please refer to our release notes for more details on what is available.
- Current supported Specification: *1.0.2*.
- Support of Class A and C devices.
- Support of **EU868** and **US915** channel frequencies.
- Activation through ABP and OTAA.
- Confirmed and unconfirmed upstream messages.
- Confirmed and unconfirmed downstream messages.
- Multi-gateways.
- Message de-duplication.
- Support of MAC commands.
- ADR Support.


  **Planned or In Development features*

## Getting Started
The no-frills easy way to deploy the components of the LoRaWAN starter kit is to use our Azure Resource Manager (ARM) template. If you rather want to perform a manual or custom setup; check out our [developer guide](/Docs/devguide.md) for a do-it-yourself version.
> Refer to our [upgrade guide](/Docs/upgrade.md)  if you are upgrading from a previous release.

### Prerequisites
The following should be completed before proceeding with the LoRaWAN starter kit development or deployment in your environment.

- You must have an Azure subscription. Get an [Azure Free account]('https://azure.microsoft.com/en-us/offers/ms-azr-0044p/') to get started.
- We are based on Azure IoT Edge so it is important that you understand the concepts and deployment model for Azure IoT Edge. Refer to Azure [IoT Edge documentation](https://docs.microsoft.com/en-us/azure/iot-edge/) to see how it works.
- Understand how LoRa and LoRaWAN works. A great primer is available at the [LoRa Alliance website](https://lora-alliance.org/resource-hub/what-lorawantm).
- To test the solution on a device, you need to have a LoRaWAN Device Kit Gateway and a LoRa end node. We have some recommendations in the [Tested Gateways](#tested-gateways) section below.

### Deploy to Azure
 Press on the button here below to start your Azure Deployment.

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fiotedge-lorawan-starterkit%2Fmaster%2FTemplate%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>


The ARM template will deploy all the required Azure infrastructure to get you started quickly. Specifically, it configures the required Azure Services and  Azure IoT Edge modules. As soon as you connect your IoT Edge device these modules will be pushed onto the gateway and you should have an Azure connected LoRaWAN gateway running. 

- The template will deploy the following resources in your Azure subscription:
  - [IoT Hub](https://azure.microsoft.com/en-us/services/iot-hub/)
  - [Azure Functions](https://azure.microsoft.com/en-us/services/functions/)
  - [Redis Cache](https://azure.microsoft.com/en-us/services/cache/)

- The LoRaWAN gateway will be provisioned with two Azure IoT Edge modules each for the [packet forwarder](https://github.com/Lora-net/packet_forwarder) and a network server.

#### Configuration for the ARM template  
- Make sure to provide your gateway's reset pin in the dialog before the deployment.
- if your gateway use SPI_DEV version 1 the packet forwarder module will not work out-of-the-box. To fix this, simply add an environment variable 'SPI_DEV' set to the value 1 to the LoRaWanPktFwdModule module (SPI_DEV is set to 2 by default).
- If you are using the the RAK833-USB, you'll need to adjust the template to use the right LoRaWan Packet Forwarder. You will find a full documentation in this [submodule](/Docs/LoRaWanPktFwdRAK833USB).

#### Installation instructions

1. The Azure template requires the following details:

- **Resource Group** - A logical "folder" where all the template resource would be put into, just choose a meaningful name.
- **Location** - In which DataCenter the resources should be deployed.
- **Unique Solution Prefix** - A string that would be used as prefix for all the resources name to ensure their uniqueness. Hence, avoid any standard prefix such as "lora" as it might already be in use and might make your deployment fail.
- **Edge gateway name** - the name of your LoRa Gateway node in the IoT Hub.
- **Deploy Device** - Do you want demo end devices to be already provisioned (one using OTAA and one using ABP). If yes, the code located in the [Arduino folder](/Arduino) would be ready to use immediately.
- **Reset pin** - The reset pin of your gateway (the value should be 7 for the Seed Studio LoRaWam, 25 for the IC880A)
- **Region** - In what region are you operating your device (currently only EU868 and US915 is supported)

  The deployment would take c.a. 10 minutes to complete.

2.  During this time, you can proceed to [install IoT Edge to your gateway](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-linux-arm).

3.  Once the Azure deployment is finished, connect your IoT Edge with the cloud [as described here](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-linux-arm#configure-the-azure-iot-edge-security-daemon). 
   > You can get the connection string by clicking on the deployed IoT Hub -> IoT Edge Devices -> Connection string.
4.  If your gateway is a Raspberry Pi, **don't forget to [enable SPI](https://www.makeuseof.com/tag/enable-spi-i2c-raspberry-pi/) , (You need to restart your pi)**.

By using the `docker ps` command, you should see the Edge containers being deployed on your local gateway. You can now try one of the samples in the [Arduino folder](/Arduino) to see LoRa messages being sent to the cloud. If you have checked the Deploy Device checkbox you can use this sample directly "TransmissionTestOTAALoRa.ino" without provisioning the device first.

## Troubleshooting
Refer to our [troubleshooting guide](troubleshooting.md) for debugging guidance, known issues along and other FAQ.

## Tested Gateways

- [Seeed Studio LoRa LoRaWAN Gateway - 868MHz Kit with Raspberry Pi 3](https://www.seeedstudio.com/LoRa-LoRaWAN-Gateway-868MHz-Kit-with-Raspberry-Pi-3-p-2823.html)
- [AAEON AIOT-ILRA01 LoRa® Certified Intel® Based Gateway and Network Server](https://www.aaeon.com/en/p/intel-lora-gateway-system-server)
- [AAEON Indoor 4G LoRaWAN Edge Gateway & Network Server](https://www.industrialgateways.eu/UPS-IoT-EDGE-LoRa)
- [AAEON AIOT-IP6801 Multi radio Outdoor Industrial Edge Gateway](https://www.aaeon.com/en/p/iot-gateway-systems-aiot-ip6801)
- [MyPi Industrial IoT Integrator Board](http://www.embeddedpi.com/integrator-board) with [RAK833-SPI mPCIe-LoRa-Concentrator](http://www.embeddedpi.com/iocards)
- Raspberry Pi 3 with [IC880A](https://wireless-solutions.de/products/radiomodules/ic880a.html)
- [RAK833-USB mPCIe-LoRa-Concentrator with Raspberry Pi 3](https://github.com/Ellerbach/lora_gateway/tree/a31d80bf93006f33c2614205a6845b379d032c57)

While we have tested the above with our starter kit, the LoRaWAN starter kit has been agnostic of a device manufacturer implementation and focussed on the specifics on underlying architectures (arm, x86). For devices and vendors not listed above, we have provided specific instructions on making  specialized hardware compatible with our kit. 
> You can follow these [device and gateway compatibility instructions](/Docs/partner.md) to enable your devices to leverage LoRaWAN starter kit.

## Contributing
If you would like to contribute to the IoT Edge LoRaWAN Starter Kit source code, please base your own branch and pull request (PR) off our [dev branch](/tree/dev/). Refer to the [Dev Guide](/Docs/devguide.md) for development instructions.

## Support
The LoRaWAN starter kit is an open source solution, it is NOT a Microsoft supported solution or product. For bugs and issues with the codebase please log an issue in this repo.

---
This project has adopted the Microsoft Open Source Code of Conduct. For more information see the [Code of Conduct overview](https://opensource.microsoft.com/codeofconduct/) or contact opencode@microsoft.com with any additional questions or comments.