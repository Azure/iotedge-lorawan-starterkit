# 002. LoRa Basic Station configuration endpoint implementation

Milestone / Epic: [#388](https://github.com/Azure/iotedge-lorawan-starterkit/issues/388)

Authors: Spyros Giannakakis, Bastian Burger

## Overview / Problem Statement

The [LNS protocol][lns-protocol] specifies an endpoint on the LoRa Network Server (LNS) that the
LoRa Basic Station (LBS) invokes to load its (own) configuration. After a successful configuration
exchange, normal operation begins. From the [protocol specification][lns-protocol] (TODO: check
copyright with legal):

> Right after the WebSocket connection has been established, the Station sends a `version` message. Next, the LNS shall respond with a `router_config` message. Afterwards, normal operation begins with uplink/downlink messages as described below.

As part of this ADR we describe how the LNS can provide the LBS with the necessary configuration. 

[lns-protocol]: https://lora-developers.semtech.com/build/software/lora-basics/lora-basics-for-gateways/?url=tcproto.html

## Requirements
- As we need to support multiple LBSs, with different hardware, in different regions etc. the
configuration returned must be dynamically populated based on the LBS provisioning.
- The update path needs to be covered. Cases here can be that the config of LBS is updated, a new
  LBS is added to the LNS or a LBS is removed. Another case that might be interesting is also what
  happens when LNS gets restarted.

## Possible solutions

We identified 4 options:
- Have Docker mounted volumes on LNS that hold the different JSON configurations for the LBSs.
- Have the LBS configuration as JSON in the LNS module twin desired properties.
- Have LBS as a child device of LNS and get's its twin properties from the twin of LNS.
- Utilize other Azure services (e.g. the existing Function, a storage etc) to store this
  configuration.

### Mounted Docker volumes

### Send configuration as part of LNS module twin

### Track each LBS as separate IoT Hub device and make devices child device of LNS

