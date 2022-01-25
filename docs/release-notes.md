---
title: Release Notes
---

## v2.0.0

### New Features

- Support for multiple Azure IoTEdge Gateways with cache and deduplication
- Support for multiple concentrators with cache and deduplication
- Support for running LNS on a separate Edge or Cloud device
- [EFLOW](https://docs.microsoft.com/en-us/azure/iot-edge/iot-edge-for-linux-on-windows?view=iotedge-2018-06) Compatibility
- Support for [LoRa Basics™ Station](https://github.com/lorabasics/basicstation)
- Regional support for CN470 and AS923
- Support for CUPS credential management
- Support for CUPS concentrator firmware update
- First class support for Class C devices
- Observability: integration with Azure Monitor and metrics that are exposed on a Prometheus endpoint

### Breaking Changes

- Deprecated support for Packetforwarder (upgrade instructions coming soon [#1271](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1271))
- Upgrade to RaspberryPi OS based on Debian 11.0 Bullseye ([upgrade instructions](https://azure.github.io/iotedge-lorawan-starterkit/dev/user-guide/upgrade/#upgrading-to-raspberry-pi-os-bullseye))

### Bugfixes

- [#249](https://github.com/Azure/iotedge-lorawan-starterkit/issues/249)
At least one resource deployment operation failed
- [#310](https://github.com/Azure/iotedge-lorawan-starterkit/issues/310)
Incorrect data type for the tmms property

## Previous Releases

Release notes of all previous release can be found on the Release page on the repository:

- [v1.0.7](https://github.com/Azure/iotedge-lorawan-starterkit/releases/tag/v1.0.7)
- [v1.0.6](https://github.com/Azure/iotedge-lorawan-starterkit/releases/tag/v1.0.6)
- [v1.0.5](https://github.com/Azure/iotedge-lorawan-starterkit/releases/tag/v1.0.5)
- [v1.0.4](https://github.com/Azure/iotedge-lorawan-starterkit/releases/tag/v1.0.4)
- [v1.0.3](https://github.com/Azure/iotedge-lorawan-starterkit/releases/tag/v1.0.3)
- [v1.0.2](https://github.com/Azure/iotedge-lorawan-starterkit/releases/tag/v1.0.2)
- [v1.0.1](https://github.com/Azure/iotedge-lorawan-starterkit/releases/tag/v1.0.1)
- [v1.0.0](https://github.com/Azure/iotedge-lorawan-starterkit/releases/tag/v1.0.0)
- [v0.4.0-preview](https://github.com/Azure/iotedge-lorawan-starterkit/releases/tag/v0.4.0-preview)
