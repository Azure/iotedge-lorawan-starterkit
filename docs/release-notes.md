---
title: Release Notes
---

## v2.2.0

### New features

- [#1746](https://github.com/Azure/iotedge-lorawan-starterkit/pull/1746): Running LoRaWAN Network Server decoupled from IoT Edge
- [#1780](https://github.com/Azure/iotedge-lorawan-starterkit/pull/1780): arm64v8 support
- [#1807](https://github.com/Azure/iotedge-lorawan-starterkit/pull/1807), [#1810](https://github.com/Azure/iotedge-lorawan-starterkit/pull/1810): AS923 Arduino examples and Documentation

### Breaking Changes

- [#1794](https://github.com/Azure/iotedge-lorawan-starterkit/pull/1794): Application Insights is now requiring a connection string instead of instrumentation key. Make sure that, when updating to this version, the `APPLICATIONINSIGHTS_CONNECTION_STRING` is set instead of obsolete `APPINSIGHTS_INSTRUMENTATIONKEY`. More information on how to [migrate from Application Insights instrumentation keys to connection strings](https://docs.microsoft.com/en-us/azure/azure-monitor/app/migrate-from-instrumentation-keys-to-connection-strings) on the official Azure documentation.

### Bugfixes

- [#1711](https://github.com/Azure/iotedge-lorawan-starterkit/pull/1711): Remove unused DevEUI that breaks deserialization
- [#1728](https://github.com/Azure/iotedge-lorawan-starterkit/pull/1728): Fix restart of the Basic Station Docker container
- [#1729](https://github.com/Azure/iotedge-lorawan-starterkit/pull/1729): Don't use rx1droffset during join accept
- [#1776](https://github.com/Azure/iotedge-lorawan-starterkit/pull/1776): Redis cache not properly updated because of IoT Hub query results delay
- Various additional fixes covered in [v2.1.0 <-> v2.2.0](https://github.com/Azure/iotedge-lorawan-starterkit/compare/v2.1.0...v2.2.0)

## v2.1.0

### New Features

- Increase scalability for [multi-gateway scenarios](./user-guide/scalability.md).
  This eliminates several disadvantages in a [multi-LNS deployment scenario](./user-guide/deployment-scenarios.md).
  See [ADR - 010. LNS sticky affinity over multiple sessions](./adr/010_lns_affinity.md).
- [Standalone discovery service](./user-guide/lns-discovery.md) for dynamic LNS discovery.
  See [ADR - 009. LoRaWAN Network Server (LNS) discovery](./adr/009_discovery.md).
- LoRaBasicsStationModule updated to Basics Station v2.0.6:
  - Support for SX1302 ([configurable](./user-guide/station-module-configuration.md) via "CORECELL" parameter)
  - Adjustable log level ([configurable](./user-guide/station-module-configuration.md) via "LOG_LEVEL" parameter)

### Breaking Changes

- [#1576](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1576): The default deduplication strategy is now "Drop" instead of "None". More information found in the [decision record](./adr/007_message_deduplication.md).

### Quality Improvements

- [#1573](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1573): Handling `ObjectDisposedException` and other exceptions by recreating the `DeviceClient`.
- [#1564](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1564): Throttling disposal of `DeviceClient`s.
- [#1540](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1540): Observe unobserved tasks.
- [#1462](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1462): Tracing of AMQP/MQTT dependencies to IoT Hub in Application Insights.
- We [enforce stricter naming conventions](https://github.com/Azure/iotedge-lorawan-starterkit/pull/1485).
- We change the way we make HTTP requests to conform with how to [make HTTP requests using `IHttpClientFactory` in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-6.0).
- When using the quickstart template, we now deploy a [Workspace-based Application Insights](https://docs.microsoft.com/en-us/azure/azure-monitor/app/create-workspace-resource) instance instead of a classic Application Insights.

### Bugfixes

- [#1344](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1344): Duplicate log statement.
- [Fixing erroneous join request count metric](https://github.com/Azure/iotedge-lorawan-starterkit/pull/1465).

## v2.0.0

### New Features

- Support for [LoRa Basics™ Station](https://github.com/lorabasics/basicstation)
LNS Protocol (v2.0.5)
- Support for [LoRa Basics™ Station](https://github.com/lorabasics/basicstation)
CUPS Protocol (v2.0.5) for credential management and firmware upgrade
- LoRaWAN Network Server and Basics™ Station can be decoupled, allowing the
possibility to connect multiple concentrators on a single LoRaWAN Network Server
instance
- Support for running LNS on a separate Edge or Cloud device
- Support for secure communication between LoRaWAN Network Server and Basics™
Station
- Support for running LoRaWAN Network Server on [Azure IoT Edge for Linux on
Windows](https://docs.microsoft.com/en-us/azure/iot-edge/iot-edge-for-linux-on-windows?view=iotedge-2018-06)
- Regional support for AS923 and CN470 (Regional Parameters v1.0.3rA and
RP002-1.0.3 revisions)
- [Observability](./user-guide/observability.md):
  - Integration with Azure Monitor
  - Exposing a Prometheus endpoint for metrics scraping
  - Collection of metrics for edgeAgent and edgeHub modules with [Metrics collector module](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-collect-and-transport-metrics?view=iotedge-2020-11&tabs=iothub)

### Code Quality Improvements

- Introduction of LoRaWAN Primitives and refactoring of existing code to make
wide use of those
- Upgrade to .NET 6 (LTS version)
- Upgrade container base images to Debian 11
- Improvements in E2E CI, continuously testing multiple scenarios and
authentication modes

### Breaking Changes

- Deprecated support for Packet Forwarder ([upgrade instructions](user-guide/pkt-fwd-to-station.md))
- Upgrade to RaspberryPi OS based on Debian 11.0 Bullseye ([upgrade instructions](user-guide/upgrade.md#upgrading-to-raspberry-pi-os-bullseye))
- Infrastructure changes:
  - New storage containers for CUPS credentials management and firmware upgrade
  (more in related [ADRs](adr/008_cups_firmware_upgrade.md))
  - A Log Analytics workspace is required for integrating with metrics collector
  (more in related [ADRs](adr/005_observability.md))
  - [Azure Functions runtime upgrade to v4](user-guide/upgrade.md/#azure-functions)

### Bugfixes

- [#249](https://github.com/Azure/iotedge-lorawan-starterkit/issues/249)
At least one resource deployment operation failed
- [#310](https://github.com/Azure/iotedge-lorawan-starterkit/issues/310)
Incorrect data type for the tmms property

## v2.0.0-beta

### Breaking Changes

- when updating from *v2.0.0-alpha*
  - Certificate Validation: In addition to validating the thumbprint, it also
  validates that the chain of trust is correct. See instructions [here](user-guide/station-authentication-modes.md#changing-client-certificate-mode-in-lorawan-network-server-module-and-trusting-certificate-chain)

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
