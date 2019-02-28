# Architecture and Concepts for the LoRaWAN starter kit

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

## High Level Architecture
Below is a high level diagram of how the framework works, for more details refer to the [dev guide](devguide.md).

![Architecture](/Docs/Pictures/EdgeArchitecture.png)

## Concepts
### Adaptive Data Rate
Solution support Adaptive Data Rate (ADR) device management as specified in [LoRa spec v1.0.2](https://lora-alliance.org/resource-hub/lorawantm-specification-v102). The main goal of the ADR is to optimize the network for maximum capacity ensuring devices always transmit with their best settings possible (highest DR, lowest power), you can find more ADR information on this [page](https://www.sghoslya.com/p/how-does-lorawan-nodes-changes-their.html). 
Adaptive Data rate is always initiated and set on the device side. ADR should never be used with moving devices. Our implementation currently implement the [Semtech proposed Algorithm for ADR](https://www.thethingsnetwork.org/forum/uploads/default/original/2X/7/7480e044aa93a54a910dab8ef0adfb5f515d14a1.pdf) and has been tested against EU868 region plan.
