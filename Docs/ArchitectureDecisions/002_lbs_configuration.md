# 002. LoRa Basic Station configuration endpoint implementation

Milestone / Epic: [#388](https://github.com/Azure/iotedge-lorawan-starterkit/issues/388)

Authors: Spyros Giannakakis, Bastian Burger

## Overview / Problem Statement

The [LNS protocol][lns-protocol] specifies an endpoint that the LoRa Basic Station (LBS) invokes an endpoint on the LNS to load its configuration. After a successful configuration exchange, normal operation begins. From the [protocol specification][lns-protocol] (TODO: check copyright with legal):

> Right after the WebSocket connection has been established, the Station sends a `version` message. Next, the LNS shall respond with a `router_config` message. Afterwards, normal operation begins with uplink/downlink messages as described below.

As part of this ADR we describe how the LNS can provide the LBS with the necessary configuration.

## Possible solutions

### Mounted volumes

Since the LNS runs as an IoT edge module, we can [use IoT Edge device local storage from a module](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-access-host-storage-from-module?view=iotedge-2020-11). We can store the configuration of all LBS in the file system of the LNS. With this approach we have no size limits, but we introduce a configuration mechanism that is not via IoT hub (= complex). To check if the configuration changed, we would need to query the configuration file periodically to detect changes and apply updates.

### Send configuration as part of LNS module twin

Adding the configuration to the device twin of the LNS would make it possible to update the LBS configuration (e.g. adding new gateways, changing existing values) by adapting the device twin of the LNS. Since device twins have a [32kb size limit for desired properties](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-device-twins#device-twin-size), this means that we will have an effective limit of LBS that we can connect to a LNS with this strategy. The device twin documentation gives us the following information:

- Property keys are encoded as UTF-8-encoded strings (usually one character takes 8 bytes)
- Boolean values take 4 bytes
- Numeric values take 8 bytes
- Complex object sizes are based on their contents

Taking as an example the configuration message from [PR #569](https://github.com/Azure/iotedge-lorawan-starterkit/pull/569), we are able to add **39** times the following LBS configuration to the device twins desired properties until we get an error: `{"NetID":[1],"JoinEui":[],"region":"EU863","hwspec":"sx1301/1","freq_range":[863000000,870000000],"DRs":[[11,125,0],[10,125,0],[9,125,0],[8,125,0],[7,125,0],[7,250,0]],"sx1301_conf":[{"radio_0":{"enable":true,"freq":867500000},"radio_1":{"enable":true,"freq":868500000},"chan_FSK":{"enable":true,"radio":1,"if":300000},"chan_Lora_std":{"enable":true,"radio":1,"if":-200000,"bandwidth":250000,"spread_factor":7},"chan_multiSF_0":{"enable":true,"radio":1,"if":-400000},"chan_multiSF_1":{"enable":true,"radio":1,"if":-200000},"chan_multiSF_2":{"enable":true,"radio":1,"if":0},"chan_multiSF_3":{"enable":true,"radio":0,"if":-400000},"chan_multiSF_4":{"enable":true,"radio":0,"if":-200000},"chan_multiSF_5":{"enable":true,"radio":0,"if":0},"chan_multiSF_6":{"enable":true,"radio":0,"if":200000},"chan_multiSF_7":{"enable":true,"radio":0,"if":400000}}],"nocca":true,"nodc":true,"nodwell":true}`. Assuming that we do not choose a different representation (e.g. compression, sharing of duplicated parts of the document) we can guarantee that we support 30 gateways per network server with this approach.

### Track each LBS as separate IoT Hub device and make devices child device of LNS



[lns-protocol]: https://lora-developers.semtech.com/build/software/lora-basics/lora-basics-for-gateways/?url=tcproto.html
