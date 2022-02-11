# 00x. LNS discovery

**Authors**: Bastian Burger, Daniele Maggio, Maggie Salak

Service (or LNS) discovery is part of the LNS protocol. When a LBS connects to the first time to a LNS, it invokes the service discovery endpoint (`/router-info`). We want to provide the users with the option to (potentially) increase availability by automatically rebalancing connection attempts to different LNS.

The LNS (either data or discovery) URI is returned in two places, namely:

- as a response as part of the CUPS protocol
- as a response of the service discovery endpoint invocation (`/router-info`)

we could consider implementing the service discovery as part of either the CUPS protocol or the LNS protocol. Since LBS do not reconnect to the CUPS endpoint when a connection to a LNS is dropped, we are only left with the option of implementing service discovery as part of the LNS service discovery endpoint.

This implies that the discovery service needs to support WebSockets. As of now, the `/router-info` endpoint is part of the LNS itself. Since we want to use it to protect against LNS outages, by definition it becomes clear that discovery needs to be isolated from the LNS and needs to be come a standalone, highly available service. In the following, we propose several properties of the discovery service.

## Decision

**Note** WIP, not final yet.

We propose to add an ASP.NET Core Web Application to the OSS starter kit that exposes an endpoint for service discovery. The Web App can deployed anywhere (as a highly available cloud service per default, or as an on-premises service for more flexibility). With respect to configuration and health probe, we will implement two simple approaches initially, and expand the functionality in a second stage.

In the initial version, we will  health check. We will rely on the fact that if a LBS reconnects to the discovery service, the LNS was not available. By using a round-robin distribution mechanism based on in-memory state, we can guarantee reasonably well that the LBS will be connected to a different LNS in a second attempt. This takes place after around two minutes.

To resolve the problem of LNS having different network boundaries, we rely on configuring the subset of available network servers through a configuration value in the station twin. We will recommend [Automatic device management at scale with Azure IoT Hub](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-automatic-device-management) for central management of stations with the same network boundaries.

In a second stage, we will prioritize one of the more advanced health probe strategies and potentially introduce more supported configuration approaches.

We will maintain the discovery endpoints on the LNS for backwards compatibility.

## Detailed analysis

### Availability

First of all, we discuss the availability of the discovery service. Even in the presence of failures of a discovery service, there are fallback mechanisms the LBS can use to attempt a direct connection to a LNS it last connected to (via `tc-bak/cups-bak` files). Still, the service discovery endpoint needs to be as highly available as possible.

- We can achieve this by choosing a highly available cloud service that acts as the service discovery endpoint.
- We can delegate the responsibility to the user in case of an on-premises deployment of the discovery service for even more flexibility.

An important restriction to consider at this point is that the service needs to support WebSockets. Since WebSockets are [not yet supported with Azure Functions](https://github.com/Azure/Azure-Functions/issues/738), we cannot use serverless Azure services, but need an always-on solution for the discovery service.

### Configuration

Due to the supported deployment models of the OSS starter kit, it is possible that only a subset of all LNS are reachable for a given LBS (due to network boundaries). Users need to configure which LBS can connect to which LNS. We have several, non-mutually exclusive options for this:

| Name                           | Description                                                  | Advantages                                                   | Disadvantages                                                |
| ------------------------------ | ------------------------------------------------------------ | ------------------------------------------------------------ | ------------------------------------------------------------ |
| Station twin                   | We can hardcode the set of LNS to which each LBS can connect to in the station twin. | - leverages  [Automatic device management at scale with Azure IoT Hub](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-automatic-device-management) for configuration<br />- similar strategy to existing configuration strategies (single place for configuration) | - (At least) one registry operation per every discovery service request<br />- Complex when not using automatic device management |
| Tags                           | We can rely on registry queries to identify LNS and stations that are in the same network | - Simple configuration (via setting a tag)                   | - Need to resolve the LNS DNS/IP based on the query results (need to add it to the LNS module twin/Edge device twin) |
| Environment/configuration file | We could statically configure the defined network boundaries as an environment variable/configuration file on the discovery service (e.g. have a JSON structure, which indicates a set of LBS/LNS that are within the same network boundaries) | - Simple for developers                                      | - Needs a discovery service restart to pick up configuration changes<br />- Configuration is spread out |

Furthermore, we must decide how to add support for distribution strategies (round-robin, random, or a more complex balancing strategy). These options are not mutually exclusive, but can be supported at the same time. We can start by using a single, simple distribution mechanism (random/round-robin based on in-memory state) and incrementally add support for different distribution strategies.

### Health checks

There are potential approaches on how to detect whether a LNS is alive or not. A simple solution would be to not keep track of the health states of the LNS. If an LBS fails to connect to a LNS, it will query the discovery service again, and we can supply a different LNS (e.g. when using round-robin distribution).

If we decide to keep track of the health of each LNS, there are several potential solutions for this:

| Name                     | Description                                                  | Advantages                                | Disadvantages                                                |
| ------------------------ | ------------------------------------------------------------ | ----------------------------------------- | ------------------------------------------------------------ |
| Bidirectional connection | Bidirectional connection (e.g. WebSocket) is permanently open between the discovery service endpoint and each LNS. |                                           | - Sensitive to transient connection drops<br />- Not necessarily a good indicator of service health (LNS can be alive but not reach discovery service) |
| D2C message              | LNS sends a periodic D2C message to indicate that it's still alive | - Also tests D2C delivery capabilities    | - Tests D2C delivery capabilities (potential problems in offline scenario) |
| Module twin status       | Discovery service relies on IoT Hub module twin for detecting module outages | - Reuses the IoT Edge monitoring strategy | From docs: he reported properties of a runtime module could be stale if an IoT Edge device gets disconnected from its IoT hub. |
| Ping discovery service   | Each LNS pings the discovery service on a regular basis to indicate that it's still alive. |                                           | - Time between crash of LNS and detection of crash           |
| State update             | LNS updates state on a regular basis, which can be queried by the discovery service, e.g. using Azure Storage blob lease management (and lease renewal) |                                           | - Time between crash of LNS and detection of crash<br />- Complexity |
| Health endpoint on LNS   | Each LNS has a health-check endpoint, that can be queried by the discovery service |                                           | - Does not necessarily work for on-premises deployments of LNS |
| No health checks at all  | The discovery service relies on the implicit knowledge that if an LBS issues another request to the discovery service, the connection to the LNS failed | - Simple                                  | - Depends on how long the station waits before backoff (and how many LNS are down)<br />- Takes around two minutes (20 seconds per default on LBS 2.0.6) on a concentrator to reconnect to the discovery endpoint if the LNS is unavailable. |

We should keep in mind that we can also use a combination of the strategies and use an incremental approach again: start with a simple health detection strategy, and allow configuring a different health detection strategy.

The selection of a health check strategy should take into account how many upstream messages can potentially be lost, depending on how long the downtime is. To investigate this further, we ran a spike where we simulated the LNS going down and then recovering after different delays, starting at 10 sec. In our experiment, the leaf device was configured to send unconfirmed messages every 5 sec. We found that in case the downtime was 1 min or shorter, we were able to recover all messages that were broadcasted in the meantime. If the downtime was over 1 min, all messages were lost.

As part of the spike we also discovered that after LNS goes down, the LBS first tries to reconnect to the data (`router-data`) endpoint for approximately 2 min. The [default timeout (`TC_TIMEOUT`)](https://github.com/lorabasics/basicstation/blob/ba4f85d80a438a5c2b659e568cd2d0f0de08e5a7/src/s2conf.h#L180) is 60 sec; the longer timeout that we saw in our experiments appears to be related to lower-level TCP timeouts. Only after around 2 min the LBS tries to connect to the discovery (`router-info`) endpoint. After reconnecting to the discovery endpoint, all upstream messages that were sent during the downtime are lost. We opened a [bug](https://github.com/lorabasics/basicstation/issues/152) describing this issue in the Basics Station repository. From the initial response on the bug it is clear that LBS is not expected to deliver messages sent during the downtime after reconnecting to the discovery endpoint.

### Auth

The discovery service needs a certificate that has a chain that is allowed by the same `tc.trust` as all the LNS it uses for balancing connections. Furthermore, all LNS need to have a chain of trust that contains the `tc.trust` of all LBS that should support a connection to a given LNS. We will document this restriction.

## Orthogonal considerations

- Monitoring and metrics we need for the discovery service (e.g. distribution to different LNS [count], how many LNS deaths we detect, invalid station connection attempt, etc.)
