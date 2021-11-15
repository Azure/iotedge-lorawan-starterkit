# Azure IoT Edge LoRaWAN Starter Kit

[![LoRa CI](https://github.com/Azure/iotedge-lorawan-starterkit/actions/workflows/ci.yaml/badge.svg?branch=master)](https://github.com/Azure/iotedge-lorawan-starterkit/actions/workflows/ci.yaml)
[![LoRa CI](https://github.com/Azure/iotedge-lorawan-starterkit/actions/workflows/ci.yaml/badge.svg?branch=dev)](https://github.com/Azure/iotedge-lorawan-starterkit/actions/workflows/ci.yaml)
[![Markdown](https://github.com//Azure/iotedge-lorawan-starterkit/actions/workflows/md-linter.yaml/badge.svg?branch=dev)](https://github.com/Azure/iotedge-lorawan-starterkit/actions/workflows/md-linter.yaml)
[![codecov](https://codecov.io/gh/Azure/iotedge-lorawan-starterkit/branch/dev/graph/badge.svg)](https://codecov.io/gh/Azure/iotedge-lorawan-starterkit)

The LoRaWAN starter kit is an OSS cross platform private network implementation
of the [LoRaWAN specification](https://lora-alliance.org/resource_hub/lorawan-specification-v1-0-2/)
built for connectivity to Azure IoT Hub. It enables users to setup their own
LoRaWAN network that can connect to LoRa based nodes (sensors) and send decoded
message packets to Azure IoT Hub for cloud based processing, analytics and other
workloads. Alternatively, it allows sending commands from the cloud to the end
nodes. The goal of the the project is to provide guidance and a reference for
Azure IoT Edge users to experiment with LoRaWAN technology.

![Architecture](/Docs/Images/EdgeArchitecture.png)

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Known Issues and Limitations](#known-issues-and-limitations)
- [Tested Gateways](#tested-gateways)
- [Support](#support)
- [Contributing](#contributing)

## Features

- LoRaWAN 1.0.2 implementation
(see [LoRaWAN Specification Support](https://azure.github.io/iotedge-lorawan-starterkit/dev/#lorawan-specification-support)
for more details)
- Device and Gateway management done completely through Azure IoT Hub.
- Bi-directional communication between LoRa end devices and Azure cloud.
- Custom packet decoding framework.
- Identity Translation for LoRa devices with caching support.
- Partial Offline and Casually connected Gateways scenarios.*
- Easy deployment and setup using Azure ARM templates.
- Small to Midsize Scalability Tests.
- Simulator for development and testing without the need to own a Gateway.

## Prerequisites

The following should be completed before proceeding with the LoRaWAN starter kit
development or deployment in your environment.

- You must have an Azure subscription. Get an
[Azure Free account](https://azure.microsoft.com/en-us/offers/ms-azr-0044p/)
to get started.
- We are based on Azure IoT Edge so it is important that you understand the
concepts and deployment model for Azure IoT Edge. Refer to Azure
[IoT Edge documentation](https://docs.microsoft.com/en-us/azure/iot-edge/)
to see how it works.
- Understand how LoRa and LoRaWAN works. A great primer is available at the
[LoRa Alliance website](https://lora-alliance.org/resource_hub/what-is-lorawan/).
- To test the solution on a device, you need to have a LoRaWAN Device Kit
Gateway and a LoRa end node. We have some recommendations in the
[Tested Gateways](#tested-gateways) section below.

## Getting Started

We have a variety of ways you can  get started with the kit, chose the
appropriate documentation based on your persona and applicability.

- **Setup a LoRaWAN Gateway**: We provide an easy to use Azure ARM template and
deployment guidance to get you quickly started with the LoRaWAN starter kit.
Use the [Quick Start](https://azure.github.io/iotedge-lorawan-starterkit/dev/quickstart/) to setup a LoRaWAN Gateway and
connect to LoRA end nodes.
- **Upgrade an existing installation**:
Refer to the [upgrade guide](https://azure.github.io/iotedge-lorawan-starterkit/dev/user-guide/upgrade/) for instructions and tips for a
clean upgrade.
- **Develop and debug the LoRaWAN starter kit**: If you are a developer and want
to contribute or customize the LoRaWAN starter kit, refer to our
[Developer Guidance](https://azure.github.io/iotedge-lorawan-starterkit/dev/user-guide/devguide/) for more details on how to build, test
and deploy the kit in your dev environment. We also support a

- **Enable a gateway or device to be compatible with the starter kit**: We have
developed the LoRaWAN starter kit agnostic of a device manufacturer
implementation and focussed on the specifics on underlying architectures
(arm, x86). However, we understand that device manufacturers can have specific
requirements; these could be specific to a gateway and the packet forwarders
they use or to the LoRa nodes and the decoders the device may use. We have
provided specific instructions on making these specialized hardware compatible
with our kit. You can follow these [instructions](https://azure.github.io/iotedge-lorawan-starterkit/dev/user-guide/partner/) depending on
your scenarios and also have your device gateway highlighted on our repo.

## Known Issues and Limitations

Refer to [Known Issues](https://azure.github.io/iotedge-lorawan-starterkit/dev/issues/) for known issues, gotchas and
limitations.

## Tested Gateways

- [Seeed Studio LoRa LoRaWAN Gateway - 868MHz Kit with Raspberry Pi 3](https://www.seeedstudio.com/LoRa-LoRaWAN-Gateway-868MHz-Kit-with-Raspberry-Pi-3.html)
- [AAEON AIOT-ILRA01 LoRa® Certified Intel® Based Gateway and Network Server](https://www.aaeon.com/en/p/intel-lora-gateway-system-server)
- [AAEON Indoor 4G LoRaWAN Edge Gateway & Network Server](https://www.industrialgateways.eu/UPS-IoT-EDGE-LoRa)
- [AAEON AIOT-IP6801 Multi radio Outdoor Industrial Edge Gateway](https://www.aaeon.com/en/p/iot-gateway-systems-aiot-ip6801)
- [MyPi Industrial IoT Integrator Board](http://www.embeddedpi.com/integrator-board)
with [RAK833-SPI mPCIe-LoRa-Concentrator](http://www.embeddedpi.com/iocards)
- Raspberry Pi 3 with [IC880A](https://wireless-solutions.de/products/radiomodules/ic880a.html)
- [RAK833-USB mPCIe-LoRa-Concentrator with Raspberry Pi 3](https://github.com/Ellerbach/lora_gateway/tree/a31d80bf93006f33c2614205a6845b379d032c57)

## Support

The LoRaWAN starter kit is an open source solution, it is NOT a Microsoft
supported solution or product. For bugs and issues with the codebase please log
an issue in this repo.

## Contributing

If you would like to contribute to the IoT Edge LoRaWAN Starter Kit source code,
please base your own branch and pull request (PR) off our dev branch.
Refer to the [Dev Guide](https://azure.github.io/iotedge-lorawan-starterkit/dev/user-guide/devguide/) for development and debugging instructions.
