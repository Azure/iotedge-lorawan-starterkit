# LoRa Basic Station configuration endpoint implementation

Milestone / Epic: [#388](https://github.com/Azure/iotedge-lorawan-starterkit/issues/388)

Authors: Spyros Giannakakis, Bastian Burger

## Overview / Problem Statement

The [LNS protocol][lns-protocol] specifies an endpoint that the LoRa Basic Station (LBS) invokes an endpoint on the LNS to load its configuration. After a successful configuration exchange, normal operation begins. From the [protocol specification][lns-protocol] (TODO: check copyright with legal):

> Right after the WebSocket connection has been established, the Station sends a `version` message. Next, the LNS shall respond with a `router_config` message. Afterwards, normal operation begins with uplink/downlink messages as described below.

As part of this ADR we describe how the LNS can provide the LBS with the necessary configuration.

[lns-protocol]: https://lora-developers.semtech.com/build/software/lora-basics/lora-basics-for-gateways/?url=tcproto.html

