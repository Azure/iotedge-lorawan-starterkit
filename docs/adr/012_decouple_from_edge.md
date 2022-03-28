# 012. Decouple LoRaWAN Network Server from IoT Edge

**Feature**: [#1553](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1553)  
**Authors**: Daniele Antonio Maggio  
**Status**: Proposed  

__________

## Problem statement

As of v2.1.0, the LoRaWAN Starter Kit makes use of Azure IoT Edge in order to host and manage LoRaWAN Network Server (LNS).  
When it comes to LNS, IoT Edge based deployments allow interesting features such as:

- centralized deployment through IoT Hub
- store and forward processing of leaf-device messages that need to be routed upstream
- management of configuration through [module twins](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-module-twins) and desired property updates
- ability to "[invoke direct methods from IoT Hub](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-direct-methods)"
- on prem data processing

With v2.0.0, the LoRaWAN Starter Kit moved from [Packet Forwarder](https://github.com/Lora-net/packet_forwarder) to a more reliable (WebSockets/TCP) and secure (mTLS capable) [Basics Station](https://github.com/lorabasics/basicstation).

This change opens up to deployment models where the LNS is hosted on a different machine from the on-premises concentrator device which is forwarding the LoRa packets.

As an example, LNS could still be hosted on-premises in a separate dedicated IoT Edge device. When it comes to scalability or high-availability, it's not always easy to provision additional IoT Edge based devices and, maybe, you could have some existing infrastructure that you might want to re-use (i.e. a Kubernetes cluster).

In addition, if you want to use Azure for hosting and scaling the LoRaWAN Network Server, in order to handle messages from different sites/locations, IoT Edge is not the proper technology to put into the cloud.

This ADR will focus on the needed changes that will allow to deploy a LoRaWAN Network Server without a strict dependency on Azure IoT Edge, while preserving functionalities like centralized configuration management and remote invocation of methods in LNS.

### In-scope

- Describe how to differentiate "edge" LNSs deployments from "cloud" ones in mixed scenarios
- Provide an alternative for deploying and managing configuration
- Provide additional information on abstracting and replacing ModuleClient related dependencies, such as:
  - Direct methods (currently used for Clearing Cache, forcefully closing DeviceClient connections, handling class-c c2d messages)
  - Twin updates (currently used for updating few configuration items, without restarting the entire LNS)
- Provide information on a "Function" endpoint for clearing cache of all "cloud" LNSs
- Describe a sample deployment scenario being enabled by decoupling

### Out of scope

- Key Vault integration for secrets management  
It would be great to make use of Azure Key Vault to securely store components like Azure Function authentication code or Server Certificate for Secure BasicsStation-LNS communications, but this document will not focus on this aspect.

## Changes required

Before diving in the various required changes, let's understand the "IoT Edge" bindings that are there in the code.

The main difference comes to "ModuleConnectionHost", which is responsible for:

- Instantiating a [ModuleClient](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.devices.client.moduleclient?view=azure-dotnet) object to be used for interacting with module twins and receiving direct method invocations
- Setting a callback on "desired property updates" for dynamically configuring some configuration variables
- Setting a callback on "direct method invocation" for handling:
  - Clearing LNS device registry cache
  - Closing DeviceClient connections for avoiding "ping-pong" as described in [ADR 010](./010_lns_affinity.md)
  - Sending Cloud to Device messages for Class-C devices

Except for ModuleConnectionHost, the other difference is concerning the ability to "proxy" leaf-device messages through Edge Hub. By default the "ENABLE_GATEWAY" environment variable is set to true, we need to make sure that **ENABLE_GATEWAY can't be set to true when not running as Edge module**.

### Differentiate Edge from Cloud deployments

As of v2.1.0, the LNS code is already able to understand whether it is running as IoT Edge Module or not. The availability of the "IOTEDGE_APIVERSION" environment variable is checked. This variable is injected by the "Edge Agent" component at the moment of module creation and startup, therefore if the variable is not there we can safely assume that the LNS is not running in a Edge environment.

When a leaf device message is sent and processed, the device twin gets updated with the "GatewayID", currently being set to the "IOTEDGE_DEVICEID" environment variable.

This information might be used by the Azure Function:

- for sending the LNS a command to close the device client connection for such leaf device
- for sending a C2D message for a Class-C device through a "direct method invocation"

We need to be able to identify in the function, **if a particular LNS is running standalone or deployed on edge**, to be able to address the LNS through different channels (edge->IoT Hub, standalone->topic).

The proposal is to:

- Prefix the "GatewayID" with "standalone-" for non-Edge deployments
- Use the "Hostname" environment variable as a default for GatewayID field

### Managing configuration and its changes

All the configuration is fetched at the start time from the environment variables.

IoT Edge allows you to specify those configuration variables as part of a template for [automatic deployments for single devices or at scale](https://docs.microsoft.com/en-us/azure/iot-edge/module-deployment-monitoring?view=iotedge-2020-11)

Depending on the target cloud service, you might have different ways to manage configuration in a centralized way.

If using Azure Kubernetes Service, a configmap to be applied to the desired cluster might work for you.
If using Azure App Service, you might just set the environment variables in the configuration section of the application.

While these solutions are great for "static" configuration (i.e.: those values that require a restart for being changed), the LNS is coded in such a way that some configuration might be changed dynamically without having to restart the entire network server.

The proposal is to **make use of "[Azure App Configuration](https://docs.microsoft.com/en-us/azure/azure-app-configuration/overview)"** to centrally manage application settings and keep [dynamic configuration using poll model](https://docs.microsoft.com/en-us/azure/azure-app-configuration/enable-dynamic-configuration-aspnet-core?tabs=core5x)

### Direct method invocation

As previously mentioned, IoT Edge is allowing the functionality of "direct method invocations" giving the possiblity to remotely invoke some functions on the LNS.

When decoupling LNS from Azure IoT Edge, there are two possibilities available for handling the scenarios covered by direct method invocation.

For both mechanisms the "Facade" Azure Function bundled with the Starter Kit, has to differentiate between a "edge" or "cloud" deployment of LNS. The proposal is to **differentiate the mechanism used for remotely invoke methods depending on the "LNS device id"**. This means that an Azure IoT Edge device will still be using the direct method invocations provided by IoT Hub whereas the "cloud" deployments will use a new, different, mechanism.

#### Expose REST endpoints on the LNS

One alternative might be to expose some REST endpoints on the LNS. This is the easiest approach to take and would simplify the "manual" invocation of such methods, even though this is not in scope of this document.

The scenarios covered by these method invocations are meant to target exactly one desired LNS instance and not any of them.

As an example, let's assume the scenario where we want to send a cloud-to-device message to a specific class-c leaf device. In this scenario we need to target the exact LNS which is managing the connection with the LoRa Basics Station in reach of the class-c device we want to target.

It would be counterproductive to invoke this method on all instances we are managing and just filter it out later (depending on the DeviceID/Hostname).

When scaling out, depending on the platform where the replicas of the LNS are hosted, you might have different ways to target exactly one replica.

On Kubernetes, for instance, this might be achieved by using a service mesh, but this requires additional configuration

#### Handle events in a Pub/Sub fashion

Another alternative might be to publish the invocation events on a topic, to which all the LNS instances are subscribing and filtering based on their "hostname".

This "polling" mechanism, compared to the "push" mechanism described before, might require some additional setup to "manually" invoke the methods, even though this is not the suggested way of invoking such methods.

The solution might be implemented using an additional service like Azure Service Bus, or by reusing the [Azure Cache for Redis](https://docs.microsoft.com/en-us/azure/azure-cache-for-redis/cache-overview) instance that it's already being deployed in the solution as it allows the usage of [Redis Pub/Sub functionality](https://redis.io/docs/manual/pubsub/)

The proposal is to **make use of Redis Pub/Sub functionality to handle remote invocation of methods in LNS**
In addition, for high scale scenarios it could be useful to **implement a new function for clearing cache of all the LNS subscribed to the topic**

## Sample "cloud" deployment scenario

![Sample AKS based deployment scenario](../images/deployment-scenario-aks.png)

The picture above highlights a possible scenario being enabled by decoupling LNS from IoT Edge.

This scenario brings in the possibility of using one (or multiple) Azure Kubernetes Services cluster for running one (or multiple) instances of LoRaWAN Network Server.

While keeping in mind that a LoRa Basics Station can be connected only to a single LNS instance at the same time, with such scenario it could be anyways possible to increase high availability or scalability.

Ideally, the LoRa Basics Station could point to a ["vNext" discovery service](./009_discovery.md) that will be able to balance the load across multiple Azure Kubernetes Service clusters, or in an "active-passive" cluster configuration, only point to the "active" one.

In addition, the load of the potentially multiple LNS instances in the same single cluster could be balanced by routing based on the number of "stations" already connected to the server instances (by leveraging the already existing metric being exported in Prometheus format)

## Summary of the required and proposed changes

- Check that ENABLE_GATEWAY environment variable is not set to true when not running as Edge module
- Conditionally set the "GatewayID" parameter depending on whether it is running on Edge or not
- Make use of "[Azure App Configuration](https://docs.microsoft.com/en-us/azure/azure-app-configuration/overview)", in "Cloud"  deployment mode, for gathering static and dynamic configuration parameters
- Make use of Redis Pub/Sub functionality, in "Cloud" deployment mode, to handle remote invocation of methods
- In Facade Azure Function, differentiate the mechanism used for remotely invoke methods depending on the "LNS device id"
  - In case of "Cloud" LNS deployment, publish invocation of methods as messages in Redis Pub/Sub
  - Only for "Cloud" LNS deployments, implement a new function for clearing cache of all the LNS subscribed to the topic
