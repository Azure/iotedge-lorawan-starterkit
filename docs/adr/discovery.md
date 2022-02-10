# 00x. LNS discovery

**Authors**: Bastian Burger, Daniele Maggio, Maggie Salak

Service (or LNS) discovery is part of the LNS protocol. When a LBS connects to the first time to a LNS, it invokes the service discovery endpoint (`/router-info`). We want to provide the users with the option to increase availability by automatically rebalancing connection attempts to healthy LNS.

The LNS (either data or discovery) URI is returned in two places, namely:

- as a response as part of the CUPS protocol
- as a response of the service discovery endpoint invocation (`/router-info`)

we could consider implementing the service discovery as part of either the CUPS protocol or the LNS protocol. Since LBS do not reconnect to the CUPS endpoint when a connection to a LNS is dropped, we are only left with the option of implementing service discovery as part of the LNS service discovery endpoint.

This implies that the discovery service needs to support WebSockets. As of now, the `/router-info` endpoint is part of the LNS itself. Since we want to use it to protect against LNS outages, by definition it becomes clear that discovery needs to be isolated from the LNS and needs to be come a standalone, highly available service. In the following, we propose several properties of the discovery service.

## Availability

First of all, the service discovery endpoint needs to be highly available. If the service discovery goes down, no LBS will be able to connect to a LNS. We could achieve this by having a highly available cloud service that acts as the service discovery endpoint.

Open questions:

- How do we support auth (server certificates on the different cloud services: Azure Functions support it based on Azure App Service)
  - If we always have the discovery service in the cloud, the LNS and the cloud discovery service need to have the same server root certificates. The `.trust` files on the LBS need to be the root cert of both the `router-info` discovery endpoint certificates and the LNS it will connect to.

## Health probes

Another challenge is how to detect that an LNS is up and running. Since LNS can either be deployed on-premises or in the cloud, we need a mechanism to let the discovery service know, which LNS are alive.

Potential solutions:

- Bidirectional connection (e.g. WebSocket) permanently open between the discovery service endpoint and each LNS.
  - Connection can be initiated by LNS (no need to open firewall rules to an on-prem LNS from the cloud)
  - Discovery service would need to be always-on (it's not possible to use consumption based services, such as Azure Functions) -> additional cost
  - Transient network failures -> very sensitive, not necessarily a good indicator since it might just be the network between LNS and discovery service had some issues.
- LNS updates state on a regular basis, which can be queried by the discovery service (e.g. , device twin, blob [potentially reuse lease refresh mechanism], Redis entry (what to do in case Redis fails - persistence?)) on a regular basis. If the last state update is too long ago, the LNS is considered to be "unavailable".
  - What happens if we drop the connection intermittently/transient failure - which "timeout" would be appropriate to consider the LNS unavailable?
  - D2C Message as a health ping/or use the state of the module (many more operations on the registry) -> also checks delivery functionality is intact, potentially with offline/temporary connection drops the discovery service might consider it as failed while it's alive (only problem for new LBS).
- LNS pings the discovery service on a regular basis to "refresh the lease" and indicate that it's still alive (= in-memory state)
- The discovery service is always-on and issues health probe requests against an endpoint on each LNS.
  - Potentially an issue with on-prem LNS due to networking
  - discovery service needs to be always on to keep track of the LNS states in-memory (or on the disk)
- ~~If serverless is a requirement due to cost constraints, we could consider [Durable Functions Overview - Azure | Microsoft Docs](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-overview?tabs=csharp#async-http). Having an orchestration per LNS and an HTTP function which queries the state of the orchestrations to indicate the liveness of an LNS.~~
- We don't do health probes but use the "let's try and see" approach from section distribution patterns.
  - How many messages will we use due to the longer delays?

Potentially support different strategies based on on-prem vs cloud-based discovery service.

## Distribution patterns

Users need a way to configure, which LBS can connect to which LNS. Potential approaches:

- Configurable distribution strategies (round-robin, random, more complex distribution)
- How do we handle partitioning (only network-related)? (e.g. the discovery service is in the cloud, but concentrators are only in reach of a subset of the LNS (since the LNS are on-prem)).
  - Potentially by environment variables in the discovery service, e.g. there is a JSON set `["gateway1(IP)", "gateway2", "lbs1", "lbs2(IP)"]` that indicates which LBS/LNS are in the same "factory/network"
  - We could add the configuration to the station twin -> causes one additional registry operation if we decide to use `router-info` based discovery.
    - could make it more generic by making use of "locations" (e.g. with tags, reported props).
- "Let's try and see": the discovery service redirects the LBS to an LNS. if the LNS is down, the LBS will issue another request to the service discovery endpoint, in which case we try the "next" LNS (either based on randomness or based on state).

## Potential solutions

### 1. CUPS (HTTP) service in the cloud, Azure Storage based health detection

- We configure in each station twin to which LNS the LBS can connect to
- We choose Azure storage (lease) based health detection: Each LNS periodically refreshes the lease (restriction: renew can only be between 15 and 60 seconds)
- When querying the CUPS endpoint in the cloud (Azure Functions based), the CUPS chooses a healthy LNS based on a (potentially configurable) distribution technique (start with: random, since it's stateless). After picking a candidate, it queries blob storage to see if the lease is active. If it is, it returns the configuration for the given LNS. If not, it retries with the next candidate LNS.
- Backwards compatibility could be maintained by keeping the CUPS endpoint on the LNS

Advantage:

- Can use consumption-based Azure Services (= cheaper and 99.95% availability). In high availability scenarios (99.99% SLA through [Azure Functions availability zone support on Elastic Premium plans | Microsoft Docs](https://docs.microsoft.com/en-us/azure/azure-functions/azure-functions-az-redundancy)), the Premium plan is necessary in any case, and the user will always have running instances.
- Low-cost

Disadvantage:

- Two additional: points of failure - Azure Functions and Azure Storage.
- Needs a storage solution for stateful distribution techniques

Condition: Does the LBS reconnect to CUPS if the LNS dies?

- https://docs.microsoft.com/en-us/azure/app-service/operating-system-functionality#file-access-across-multiple-instances)

### 3. Always-on web app (either on-prem or in the cloud)

- Initial version: let's try and see
- Configuration: either configuration via twin or via environment variables (or maybe both?)
- Health probes:
  - Module state via twin
  - D2C message health checks
  - LNS -> HTTP Request -> Discovery Service, Discovery keeps track (in-memory) of the last successful health probe
  - Use a combination of several strategies

## Orthogonal considerations

- The discovery service needs to validate that the LBS is indeed a configured LBS (needs to query IoT Hub registry)
- Authn/Authz: which certs are valid for the discovery endpoint? tc-boot/trust
- Monitoring and metrics we need for the discovery service (e.g. distribution to different LNS [count], how many LNS deaths we detect, invalid station connection attempt, etc.)
- Do we want to support an opt-in/opt-out possibility (backwards compatible) of using a separate discovery service?

Spikes:

- Does the LBS reconnect to CUPS if the LNS dies?
- How many messages will we lose due to the longer delays?



