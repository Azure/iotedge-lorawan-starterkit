# 009. LNS sticky affinity over multiple sessions

**Feature**: [#1475](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1475)  

**Authors**:

**Status**: Proposed
__________

## Problem statement

Consider the scenario:

```mermaid
flowchart LR;
    Device-->LBS1-->LNS1--1-->IoTHub;
    Device-->LBS2-->LNS2--2-->IoTHub;
```

where LNS 1 and 2 make use of their IoT Edge Hub modules to connect to IoT Hub.

IoT Hub limits active connections that an IoT device can have to one. Imagining that connection 1 is
already open and a message from LNS2 arrives, IoT Hub will close connection 1 and open connection 2.
Edge Hub on LNS1, will detect this and assume it's a transient network issue, therefore will try
proactively to reconnect to IoT Hub. IoT Hub will now drop the connection 2 to re-establish the
original connection 1. This connection "ping-pong" will continue happening, negatively impacting the
scalability due to the high costs of setting up/disposing the connections. From our load tests we
observed that in this scenario we were not even able to connect 500 devices to the same concentrator,
while in a single LNS topology we could scale up to 900 devices without issues.

## Out of scope

- Deduplication strategies Mark and None: these strategies rely on multiple LNSs sending messages
  which by definition goes against the IoT Hub limitation of a single connection per device.
  Conceptually we find it is acceptable for the Mark and None strategies to not be as scalable as
  the Drop strategy and will only document this limitation for potential users to be aware of.

- Class C devices are not facing this issue since for C2D messages as they are using the module
  client​ - TODO verify this claim ❔

## Limitations

- We should avoid introducing additional calls to the Function as this would also hurt scalability.
  
## In-scope

The problem can be manifested whenever we do operations against Iot Hub. These can be:

- Twin reads
- Twin writes (updates/deletes)
- D2C messages
- C2D messages

These operations can be performed on behalf of a device/sensor or a concentrator/station. However since a
concentrator can be connected to at most 1 LNS, there is no ping-pong happening with operations on stations.

The ping-pong occurs only if we use the DeviceClient - not if we use the registry manager (as the
Function does).

The Network Server does the following things that are relevant with regards to IoT Hub connections. Changes required are indicated with ⭕ and open questions with ❔

- ~~During server startup, checks the certificate thumbprints against the twin (twin read) when LnsServerPfxPath. (is this~~
  ~~actually enabled in the code now❔) This twin is not the twin of the device but of the station and~~
  ~~a station can connect only to a single LNS so it should not result in a connection ping-pong. Also server startup happens once (under normal operation).~~
- Once the server is up:
  - Discovery endpoint: no twin reading/writing or C2D/D2C message ✔
  - Data message endpoints:
    - version -potentially get primary key from the Function (uses the registry manager), perform a
      station twin write (version) then a station twin read ✔
    - join - station twin read (for the region), DeviceGetter.GetDevice
      Function (uses registry manager), UpdateAfterJoinAsync writes on the device twin ⭕
    - data - station twin read (for the region), BeginDeviceClientConnectionActivity already creates
     a deviceclient ⭕, if device resets we update the twin, then call the bundler..
  - CUPS updateInfo endpoint: station read twin (for CUPS property), then calls the Function
    RunFetchConcentratorFirmware/Credentials for the station ✔

❔ Can we assume that if we lose the race on the first problematic operation against IoT Hub we
should abandon all further operations?  

Yes, assuming we choose the right threshold.

## Possible solutions

We need to ensure that only a single LNS has an active connection for a device at a given time among
both the Join and Upstream data endpoint.

### Delayed processing of messages from losing LNSs

The main idea here is to delay the processing of future messages for all gateways *besides* the
winning one. This should give enough time to that chosen LNS to process the message and keep the
active connection to Iot Hub. The Function is already the single point where data messages get
deduplicated before being sent upstream. What is changing here is the way we react to the response
from the Function.

#### Example scenario in main data message flow

Assuming the topology:

```mermaid
flowchart LR;
    Device-->LBS1-->LNS1-->Function;
    Device-->LBS2-->LNS2-->Function;
    LNS1-->IoTHub;
    LNS2-->IoTHub;
```

where Device sends data message A and then B.

Here is a rundown of what should happen:

- Device sends first data message A.
- Assuming that LNS1 gets the message first and since it hasn't seen this DevEui before, it contacts
  the Function.
- The Function hasn't seen this DevEui either and therefore does not have an assigned LNS for it
  yet. LNS1 wins the race and gets immediately a response and processes the message upstream.
- LNS2 eventually receives message A and also contacts the Function since it does not have prior
  info about this devEui.
- The function responds to LNS2 that it lost the race to process this message.
- ⭕ Since deduplication strategy is Drop, LNS2 drops the message immediately, therefore no
  connection to Iot Hub is opened and only LNS1 has the connection to Iot Hub. It also notes in
  memory that it was the losing gateway for this DevEui.
- ⭕ When message B gets send (with a higher frame counter), assuming that this time LNS2 gets it
  first it sees that it's not the preferred LNS for this device and therefore delays itself X ms.
- This delay gives LNS1 a time advantage to reach the Function first and win the race again, failing
  back to the previous case of message A. The active connection stays with LNS1 in this case.
  - If this delay is not sufficient for LNS1 to win the race (because LNS1 crashed or is out of
    range etc), LNS2 will contact the Function which now award LNS2 as the "winning" LNS. LNS2 will
    process message upstream (therefore the active connection will switch to it) and will remove the
    "losing flag" from the in-memory store.
  - When/if LNS1 gets message B and contacts the Function, it will let it know that it lost the race
    for this frame counter and must therefore drop the message and mark itself as the losing LNS, as
    LNS2 did for message A.
  
Another variant here is that all LNSs contact the function immediately and the function keeps the
losing LNS in its state. The Function then delays sending the response to the losing LNSs.

Open questions: how to handle the case of a device reset between message A and B? We save the twin
immediately (irrelevant but how does the Function get it for the deduplication decision?)

#### Join request flow needs to be adjusted as well 

TODO:
Potentially we could already check in DeviceGetter.GetDevice if we are the winning or losing LNS.

#### Feature flag

The stickiness feature can be disabled with a feature flag on the LNS configuration.

- When flag is set, LNS does not change its behavior and connection ping-pong happens.
- This flag is only checked if we are on the Drop deduplication strategy (as Mark and None do not
  support stickiness anyway)

#### Consequences / implementation

- We should store additional state on the LNS itself or the function about which one is the winning
  LNS.

Where shall we store the info that we are the losing one? Can be on the:

- LNS LoRaDeviceClient.ConnectionManager since all of  the operations pass through it.
- On the Function

### Using direct mode (not Edge hub)

The [IoT Edge hub
module](https://docs.microsoft.com/en-us/azure/iot-edge/iot-edge-runtime?view=iotedge-2020-11#iot-edge-hub)
is responsible for communication, acting as a man-in-the-middle to IoT Hub. One of the major
features it offers is [offline
support](https://docs.microsoft.com/en-us/azure/iot-edge/offline-capabilities?view=iotedge-2020-11#how-it-works)
that ensures messages are not lost and communication can still happen between child devices even
when connectivity to Iot Hub is lost. When connectivity resumes it ensures the communication
continues normally.

In scenarios where scalability is a priority and dropped messages are acceptable, it could be
evaluated to not pass messages through Edge hub but instead use direct mode to connect to Iot Hub.

### Single threaded access to single message queue

TODO

## Other candidates

### Parent-child gateways

On deployments where multiple gateway servers are used, they need to have a parent 

Child LNS A ____ | Parent Gateway using AMQP maintains a single connection to IoTHub | Child LNS B
                ____|

Not possible due to single parent limitation (not supporting roaming LNSs)

## Questions

- The ping pong occurs only when we are going through the local Edge Hub module? why is that? Edge
  hub tries to reconnect for us to keep the cache fresh.
