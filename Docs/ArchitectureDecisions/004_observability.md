# 003 - Observability

**Date**: 2021-11-08

**Participants**:  Patrick Schuler, Bastian Burger, Eugene Fedorenko

## Status

Accepted

## Context

The goal of observability for the LoRaWAN IoT Edge starter kit is to:

1. Monitor if the LoRaWAN Starter Kit solution works according to the user expectations regarding the following factors:
   - Coverage. The data is coming from the majority of observed IoT assets
   - Freshness. The data coming from the assets is fresh and relevant
   - Throughput. The data is delivered from the assets without significant delays.
   - Correctness. The ratio of errors and lost messages from the assets is small
1. Provide monitoring instruments to detect possible failure/violation in each factor
1. Provide instruments to identify and diagnose failures to get to the problem quickly

The decisions in the following will apply to our LoRaWAN Network Server (LNS) implementation.

## Decisions

We will support Azure Monitor as a first-class monitoring solution for our starter kit. A user can opt-in to use Application Insights with the starter kit, in which case we will support a rich set of observability features. If the user decides to not use Application Insights, we will still support essential monitoring capabilities. This means that we will:

- Track LNS logs in Application Insights (when opted in). We will adhere to the [IoT Edge recommended format](https://docs.microsoft.com/en-gb/azure/iot-edge/how-to-retrieve-iot-edge-logs?view=iotedge-2020-11#recommended-logging-format) for the structure of the log console output. Export of logs to anything else than Application Insights requires a custom solution by the user and is not supported by the starter kit.
- Always expose metrics using [prometheus-net](https://github.com/prometheus-net/prometheus-net).
  - Additionally, we track LNS metrics using the ASP.NET Core Application Insights SDK (when opted in)
- Track traces using the Application Insights SDK (when opted in)
- Support alerts when using Application Insights
- For now we will not support complete distributed tracing in the LoRaWAN starter kit, other than what Application Insights tracing will give us out of the box. We will evaluate this with [#695](https://github.com/Azure/iotedge-lorawan-starterkit/issues/695).

A more thorough description of each bullet point follows below.

### Logs

Using `ILogger` as the core method to log information from all parts of the application makes sure we have an abstracted logging framework we can use and can add/remove sinks as required.

The different log sinks are implemented as `ILoggerProvider`. We will have three to start with:

1. Console
2. IoT Hub
3. UDP

The standard logger for Application Insights is added on an opt-in basis. We will adhere to [the recommended logging format](https://docs.microsoft.com/en-gb/azure/iot-edge/how-to-retrieve-iot-edge-logs?view=iotedge-2020-11#recommended-logging-format) for the LNS console logger to comply with the IoT Edge log format and to simplify logs scraping. We will not support a full logs delivery solution, such as [ELMS](https://github.com/Azure-Samples/iotedge-logging-and-monitoring-solution), since it will introduce too many components and too much complexity to the starter kit. This means that we will not support cloud delivery of edgeAgent and edgeHub logs other than what is documented in [Retrieve IoT Edge logs - Azure IoT Edge | Microsoft Docs](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-retrieve-iot-edge-logs?view=iotedge-2020-11).

If a user of the starter kit wants to scrape logs from modules other than LNS, or use a service other than the Application Insights SDK, the user will have to implement a custom solution.

### Traces

We use built-in tracing from Azure Application Insights (on an opt-in basis). This works well for function calls and correlation to other services, such as Key Vault. We will not include message flow end to end tracing for now, but will reevaluate with [#695](https://github.com/Azure/iotedge-lorawan-starterkit/issues/695).

### Metrics

The core modules `edgeHub` and `edgeAgent` support emitting metrics through a Prometheus endpoint, using the strategy described in [Access built-in metrics - Azure IoT Edge | Microsoft Docs](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-access-built-in-metrics?view=iotedge-2020-11). To collect these metrics and integrate everything with Application Insights, we use the metric collector (preview) as suggested in [Collect and transport metrics - Azure IoT Edge | Microsoft Docs](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-collect-and-transport-metrics?view=iotedge-2020-11&tabs=iothub) to export metrics to a Log Analytics storage.

We will always expose LNS custom metrics in Prometheus format using [prometheus-net/prometheus-net](https://github.com/prometheus-net/prometheus-net), such that they can be consumed by any scraper that supports the Prometheus format. This will give us the following features:

- Unified metrics format accross all modules in the Edge device. 
  - The Prometheus format is industrial standard understood by various consumers.
- Decouples metrics exposure from the delivery-to-cloud approach. If at one point we decide to change how we scrap the metrics or how/where we deliver them to the observer, we can do that without changing the modules.
- Eliminates any dependencies on Azure Monitor services (Log Analytics / Application Insights) for essential monitoring
- Potentially gives ability to work offline if metrics are sent by the collector module through the Edge Hub using device-to-cloud channel. 
- It's up to the customer to configure how, where and what metrics to deliver from any module on an edge device.

In addition to this, we will support Application Insights metrics on an opt-in basis. When enabled, we will deliver most metrics (custom and default from LNS, except the edgeAgent and edgeHub metrics, which can only be delivered to Log Analytics) to Application Insights. This will ensure that we get many of the features that we get with Application Insights out of the box (Live Metrics, integration with alerts and workbooks), while still keeping the flexibility of consuming the metrics in Prometheus format and all the advantages that come with it. This comes at the cost of increased implementation complexity.

#### Custom metrics/events

| Name                       | Description                                                  | Source | Namespace | Dimensions                 |
| -------------------------- | ------------------------------------------------------------ | ------ | --------- | -------------------------- |
| MessageDeliveryLatency     | Time from when we received the message from the concentrator until we are done processing it - including downstream send. | LNS    | LoRaWan   | Gateway Id                 |
| RxWndRate                  | Number of times we hit the different receive windows.        | LNS    | LoRaWan   | Gateway Id, Receive Window |
| RxWndMiss                  | Number of missed on downstream windows                       | LNS    | LoRaWan   | Gateway Id                 |
| DeviceCacheHit             | Number of device cache hit                                   | LNS    | LoRaWan   | Gateway Id                 |
| DeviceLoadRequests         | Number of device load requests                               | LNS    | LoRaWan   | Gateway Id                 |
| JoinRequests               | Number of join requests                                      | LNS    | LoRaWan   | Gateway Id                 |
| D2CMessagesReceived        | Number of messages received from device                      | LNS    | LoRaWan   | Gateway Id, Device Id      |
| D2CMessagesDelivered       | Number of messages sent to upstream                          | LNS    | LoRaWan   | Gateway Id, Device Id, To  |
| D2CMessagesError           | Number of errors in sending messages to upstream             | LNS    | LoRaWan   | Gateway Id, Device Id, To  |
| D2CMessagesProcessingError | Number of errors processing (decoding, decrypting) messages  | LNS    | LoRaWan   | Gateway Id, Device Id, To  |
| D2CMessageSize             | Message size in bytes received from device                   | LNS    | LoRaWan   | Gateway Id, Device Id      |
| D2CMessageSizeUpstream     | Message size in bytes sent upstream                          | LNS    | LoRaWan   | Gateway Id, Device Id, To  |

### Alerts

We support the following alerts when the user opts in to use Application Insights.

| Name                              | Description                                                  | Source                                   | Condition |
| --------------------------------- | ------------------------------------------------------------ | ---------------------------------------- | --------- |
| HighDeviceLastSeenTime            | Alerts when high device last seen time (coverage/availability, freshness). Might not be easy due to throttling of logs, maybe omit in the beginning. | D2CMessagesReceived metric               | Dynamic   |
| LowDeviceUpstreamMessageFrequency | Alerts when low device upstream message frequency detected (messages per min) (freshness, throughput) | D2CMessagesDelivered metric              | Dynamic   |
| HighDeviceMessageLatency          | High device message processing time (throughput)             | MessageDeliveryLatency                   | Dynamic   |
| HighDeviceMessageErrorRatio       | High device messages error ratio (correctness)               | D2CMessagesError                         | Dynamic   |
| HighDeviceMessagesLostRatio       | High device messages lost ratio (correctness, throughput)    | D2CMessagesReceived/D2CMessagesDelivered | Dynamic   |

## Alternatives considered

As a generic alternative to the Application Insights SDK we considered the OpenTelemetry .NET SDK. This would allow us to abstract emitting telemetry for different backend systems. However, the status of the project - [open-telemetry/opentelemetry-dotnet: The OpenTelemetry .NET Client (github.com)](https://github.com/open-telemetry/opentelemetry-dotnet) - is not ready to be added to the Starter Kit. Especially Prometheus exporter (alpha) and metrics in general (experimental) do not help us improving our solution at the moment.
