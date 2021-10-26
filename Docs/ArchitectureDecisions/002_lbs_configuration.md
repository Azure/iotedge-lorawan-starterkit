# 002. LoRa Basic Station configuration endpoint implementation

Milestone / Epic: [#388](https://github.com/Azure/iotedge-lorawan-starterkit/issues/388)

Authors: Bastian Burger, Spyros Giannakakis

## Overview / Problem Statement

The [LNS protocol][lns-protocol] specifies an endpoint that the LoRa Basic Station (LBS) invokes an endpoint on the LNS to load its configuration. After a successful configuration exchange, the LBS/LNS start to operate normally. From the [protocol specification][lns-protocol]

> Right after the WebSocket connection has been established, the Station sends a `version` message. Next, the LNS shall respond with a `router_config` message. Afterwards, normal operation begins with uplink/downlink messages as described below.

As part of this ADR we describe how the LNS can provide the LBS with the necessary configuration. We
analyze different strategies with respect to their implementation simplicity, deployment simplicity
and how to detect updates to the configuration.

## Restrictions / limitations
- Twins have a limit of 32KB for desired properties.
- [Children devices](https://docs.microsoft.com/en-us/azure/iot-edge/iot-edge-as-gateway?view=iotedge-2020-11#parent-and-child-relationships) can only have one parent and a parent can have up to 100 children devices. 

## Possible solutions

### Option 1: Mount volume on LNS with the LBS configuration

Since the LNS runs as an IoT edge module, we can [use IoT Edge device local storage from a module](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-access-host-storage-from-module?view=iotedge-2020-11). We would store the configuration of all LBSs in the file system of the LNS. 

*Advantages*:
- Implementation effort is low.
- No limitation in terms of how many LBSs can connect to an LNS. 

*Disadvantages*:
- An additional configuration mechanism besides the IoT Hub is introduced to the system. This would
  complicate the provisioning flow and potentially require the involvement of a separate infra team. 
- Deployment of updated configuration is not straightforward, since for configuration updates we
  need access to the file system of where the LNS is hosted. To check if the configuration changed,
  we would also need to query the configuration file(s) periodically to detect changes and apply updates.

### Option 2: Hold LBS configurations in the LNS module twin

Adding the configuration to the module twin of the LNS would make it possible to update the LBS configuration (e.g. adding new gateways, changing existing values) by adapting the module twin of the LNS. Since device twins have a [32kb size limit for desired properties](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-device-twins#device-twin-size), this means that we will have an effective limitation in terms of how many LBS we can connect to a single LNS. The device twin documentation states the following about the encoding of the twin desired properties:

- Property key is encoded as UTF-8-encoded string (usually one character takes 8 bytes)
- Boolean value takes 4 bytes
- Numeric value takes 8 bytes
- Nested object size is based on their content

Taking as an example the configuration message from [PR #569](https://github.com/Azure/iotedge-lorawan-starterkit/pull/569), we are able to add **37** times the following LBS configuration to the device twins desired properties until we get an error: `{"msgtype":"router_config","NetID":[1],"JoinEui":["9223372036854775807","9223372036854775807"],"region":"EU863","hwspec":"sx1301/1","freq_range":[863000000,870000000],"DRs":[[11,125,0],[10,125,0],[9,125,0],[8,125,0],[7,125,0],[7,250,0]],"sx1301_conf":[{"radio_0":{"enable":true,"freq":867500000},"radio_1":{"enable":true,"freq":868500000},"chan_FSK":{"enable":true,"radio":1,"if":300000},"chan_Lora_std":{"enable":true,"radio":1,"if":-200000,"bandwidth":250000,"spread_factor":7},"chan_multiSF_0":{"enable":true,"radio":1,"if":-400000},"chan_multiSF_1":{"enable":true,"radio":1,"if":-200000},"chan_multiSF_2":{"enable":true,"radio":1,"if":0},"chan_multiSF_3":{"enable":true,"radio":0,"if":-400000},"chan_multiSF_4":{"enable":true,"radio":0,"if":-200000},"chan_multiSF_5":{"enable":true,"radio":0,"if":0},"chan_multiSF_6":{"enable":true,"radio":0,"if":200000},"chan_multiSF_7":{"enable":true,"radio":0,"if":400000}}],"nocca":true,"nodc":true,"nodwell":true}`. Assuming that we do not choose a different representation (e.g. compression, sharing of duplicated parts of the document) we can safely guarantee that we support 30 gateways per network server with this approach.

*NB*:
- Keeping track of configuration changes is equivalent to keep track of changes to the desired properties by using `ModuleClient.SetDesiredPropertyUpdateCallbackAsync`
- In case the specification requires us to transfer 64-bit numbers (e.g. for EUIs), we either need to split up such a number into two 32-bit numbers or represent it as a string. Device twins encode numbers as 32-bit values.

*Advantages*:
- We can observe configuration changes without needing to fetch the contentrator device key (as in
  Option 3).
- No need to create additional IoT devices for each LBS.
- Configuration of all LBSs is centralized in one place which would make it easier to find.  

*Disadvantages*:
- The centralization of configuration complicates the changes needed to connect to other or multiple
  LNSs (for dynamic discovery or resiliency purposes)
- The limit of ~30 LBSs per LNS is considered enough for now (initially we considered 4-5 LBSs per
  LNS) but could be limiting in the future especially if the twins grow.
  - Workarounds can be considered e.g. use this space only for non-default LBSs or potentially compress

### Option 3: Track each LBS as separate IoT Hub device

In this option, we create IoT Hub devices (not edge-enabled) for every LBS. The configuration of LBS
is held in the device twin.

*Open question*:
- Do we allow an LNS to get info for any LBS or only for the ones that are connected to it?

#### LNS uses the Azure Function to retrieve the device key

- LBS requests its configuration passing its device id.
- LNS invokes the existing Azure Function to get the Device key passing the LBS id.
- The Function returns the Device Key (via a cache or by querring IoT Hub).
- LNS uses the Device Key to impersonate LBS, get its twin that holds the configuration and any
  future updates to it by `DeviceClient.SetDesiredPropertyUpdateCallbackAsync`.

Notes: Code changes are not extensive, as most of the code change is already there

*Advantages*:
- LBSs are self-contained and therefore can be moved more easily for example to a different LNS (discovery
  phase) or connect to more than one LNS (resiliency).

*Disadvantages*:
- We would need to provision and manage IoT devices for LBSs.
- More complex LNS flow since it involves the Function.
- Possible delay, though there is no timeout window within which we need to respond. 
- Multiple open connections are required for each LBS.

#### Alternative 1: utilize the child-parent feature to avoid invoking additional Azure services

By establishing a [child-parent
relationship](https://docs.microsoft.com/en-us/azure/iot-edge/iot-edge-as-gateway?view=iotedge-2020-11#parent-and-child-relationships)
between LBSs and LNS, messages from and to LBS are passed transparently through the LNS. The idea
here would be that LNS as the parent, has access to the LBS device twin __without__ the need to use
the LBS device key. 

Reason for disqualifying: There seems that there is no API in either the
[DeviceClient](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.devices.client.deviceclient?view=azure-dotnet)
nor the
[ModuleClient](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.devices.client.moduleclient?view=azure-dotnet)
to retrieve the device twin of a child.

#### Alternative 2: edge-enabled LBS sends upstream its twin to LNS

If LBS was an IoT Edge enabled device, we could utilize
`ModuleClient.SetDesiredPropertyUpdateCallbackAsync` to send upstream to its parent the updated
module twin. LNS could filter messages coming from downstream/child devices based on whether they contain
a module id or not to get a hold of this configuration.  

Reason for disqualifying: Given that LBS needs to run on low powered devices, we can not change them to IoT Edge enabled devices.

## Outcome

Based on this investigation and the team discussion, [Option 3](#option-3-track-each-lbs-as-separate-iot-hub-device) was chosen due to the flexibility it
offers in terms of use-cases (resiliency, dynamic discovery) as well as scaling options. The
additional complexity is deemed reasonable given the additional future-proofness.

[lns-protocol]: https://lora-developers.semtech.com/build/software/lora-basics/lora-basics-for-gateways/?url=tcproto.html
