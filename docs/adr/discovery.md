# 00x. LNS discovery

**Authors**: Bastian Burger, Daniele Maggio, Maggie Salak

Service (or LNS) discovery is part of the LNS protocol. When a LBS connects to the first time to a LNS, it invokes the service discovery endpoint (`/router-info`). We want to provide the users with the option to increase availability by automatically rebalancing connection attempts to healthy LNS.

The LNS (either data or discovery) URI is returned in two places, namely:

- as a response as part of the CUPS protocol
- as a response of the service discovery endpoint invocation (`/router-info`)

we could consider implementing the service discovery as part of either the CUPS protocol or the LNS protocol. Since LBS do not reconnect to the CUPS endpoint when a connection to a LNS is dropped, we are only left with the option of implementing service discovery as part of the LNS service discovery endpoint.

This implies that the discovery service needs to support WebSockets. As of now, the `/router-info` endpoint is part of the LNS itself. Since we want to use it to protect against LNS outages, by definition it becomes clear that discovery needs to be isolated from the LNS and needs to be come a standalone, highly available service. In the following, we propose several properties of the discovery service.

## Proposed solution

We propose to add an ASP.NET Core Web Application to the OSS starter kit that exposes an endpoint for service discovery. The Web App can deployed anywhere (as a highly available cloud service per default, or as an on-premises service for more flexibility).

- Initial version: let's try and see
- Configuration: either configuration via twin or via environment variables (or maybe both?)
- Health probes:
  - Module state via twin
  - D2C message health checks
  - LNS -> HTTP Request -> Discovery Service, Discovery keeps track (in-memory) of the last successful health probe
  - Use a combination of several strategies

## Detailed analysis

### Availability

First of all, we discuss the availability of the discovery service. Even in the presence of failures of a discovery service, there are fallback mechanisms the LBS can use to attempt a connection to the LNS (see `tc-bak/cups-bak` files). Still, the service discovery endpoint needs to be as highly available as possible.

- We can achieve this by choosing a highly available cloud service that acts as the service discovery endpoint.
- We can delegate the responsibility to the user in case of an on-premises deployment of the discovery service for even more flexibility.

An important restriction to consider at this point is that the service needs to support WebSockets. Since WebSockets are [not yet supported with Azure Functions](https://github.com/Azure/Azure-Functions/issues/738), we cannot use serverless Azure Service, but need an always-on solution for the discovery service.

### Configuration

Due to the supported deployment models of the OSS starter kit, it is possible that only a subset of all LNS are reachable for a given LBS (due to network boundaries). Users need to configure which LBS can connect to which LNS. We have several, non-mutually exclusive options for this:

- We can hardcode the set of LNS to which each LBS can connect to in the station twin. As of now, we already store the CUPS configuration in the station twin (which also contains the TC URI configuration value). Adding a set of supported LNS (potential using [Automatic device management at scale with Azure IoT Hub](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-automatic-device-management)) does not introduce another location for the configuration, but keeps the config at the same location.
  - This causes an additional device twin operation per connection request to the discovery service
- We can use tags or reported properties to associate LBS with LNS that are within the network boundaries.
- We could statically configure the defined network boundaries as an environment variable/configuration file on the discovery service (e.g. have a JSON structure, which indicates a set of LBS/LNS that are within the same network boundaries).

Furthermore, we must decide how to add support for how distribution strategies (round-robin, random, or a more complex balancing strategy). These options are not mutually exclusive, but can be supported at the same time. We can start by using a single, simple distribution mechanism (random/round-robin based on in-memory state) and incrementally add support for different distribution strategies.

### Health probes

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
| No health probes at all  | The discovery service relies on the implicit knowledge that if an LBS issues another request to the discovery service, the connection to the LNS failed | - Simple                                  | - Depends on how long the station waits before backoff (and how many LNS are down) |

We should keep in mind that we can also use a combination of the strategies and use an incremental approach again: start with a simple health detection strategy, and allow configuring a different health detection strategy.

### Auth

The discovery service needs a certificate that has a chain that is allowed by the same `tc.trust` as all the LNS it uses for balancing connections. Furthermore, all LNS need to have a chain of trust that contains the `tc.trust` of all LBS that should support a connection to a given LNS. We will document this restriction.

## Orthogonal considerations

- The discovery service needs to validate that the LBS is indeed a configured LBS (needs to query IoT Hub registry)
- Authn/Authz: which certs are valid for the discovery endpoint? tc-boot/trust
- Monitoring and metrics we need for the discovery service (e.g. distribution to different LNS [count], how many LNS deaths we detect, invalid station connection attempt, etc.)
- Do we want to support an opt-in/opt-out possibility (backwards compatible) of using a separate discovery service?

Spikes:

- Does the LBS reconnect to CUPS if the LNS dies?
- How many messages will we lose due to the longer delays?



