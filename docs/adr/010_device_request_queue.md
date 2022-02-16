# Device Request Queue

- **Feature**: [#1480]
- **Author(s)**: Atif Aziz
- **Status**: Proposed

## Context

LoRa devices, which can reach multiple LNS, pose an interesting challenge for
Edge Hub since Edge Hub wasn't designed for devices competing for gateways.
When a device connection is made to IoT Hub, Edge Hub continues to maintain
that connection in the open state (for network resiliency) until explicitly
closed. Moreover, IoT Hub allows maintining only a single connection per
device. When another is opened, the existing one closed. If a device can reach
multiple LNS then Edge Hub and IoT Hub begin an aggressive ping-pong of
connections being opened and closed. In other words, as each LNS or Edge Hub
tries to open a connection for the same device, IoT Hub closes the connection
for the other. But then the other tries to re-open the connection and the
ping-pong continues. This problem needs addressing to avoid scalability
limitations.

The ping-pong can be pevented by electing a leader LNS for a device and
announcing the decision to others using a C2D message. An LNS receiving such a
message will then close that connection so that Edge Hub does't continue to
maintain the connection in the open state through retires. The handling of the
message needs to know:

1. It can close the connection right now, without interfering with a currently
   running operation (like a message delivery where the message could be
   potentially dropped).

1. There are no operations starting around the same time that would cause it
   to be opened again.

The following cases can trigger operations from different threads:

- An OTAA join message; `DeviceJoinLoader` eventually loads the twins and
  stores them back up.

- A data message; `DeviceLoaderSynchronizer` loads the twins for multiple
  devices based on the `DevAddr` and adds them to the cache.

- C2D message that needs to be delivered can trigger a load if the device is
  not in the cache.

- Cache refreshes can happen on a background thread for a device, causing the
  need for a connection.

**Note**: We should consider the option to keep building on top of the connection
locking/counting we have today.

There are a number of complex and concurrent code paths in the current
solution that make it hard to reason about when a device connection is in use,
when it's safe to open/close without affecting another operation in flight and
how various device-related operations (such as refreshing from the twin) can
work in isolation, deterministically and without race conditions. If a single
queue could be maintained for all device-related operations then it would
become easier to order those operations, reason about them and make they don't
cause connections to be opened when an LNS has lost the race against another.

Several approaches were explored to understand the overall impact of
refactoring, whether the changes to the current code base and design would be
large or small and worth the benefits they bring. The approaches explored can
be summed up as follows:

1. Add a queue to `LoRaDevice` for all device-related operations. There is one
   that exists today but it is used to service uplink data requests only. The
   idea would be to extend it to encompass all other requests and operations.

1. Turn the LNS implementation to be (logically) single-threaded so that not
   only does it make it easier to reason about the code (except perhaps with
   regards to re-entrancy), but it also enables use of very simple data
   structures without the need for locking. This is possible because the bulk
   of LNS is I/O bound. There is practically no CPU-intensive code that
   executes between each await.

1. Simplify the entire flow and processing of a message into a simple
   request-response model that can be manipulated and reasoned about much more
   easily. That is, all methods receiving and processing a `LoRaRequest`
   return `Task<DownlinkMessage>` instead of `void` or `Task`. This allows all
   `Task.Run` uses to be moved to a single and central point when a message is
   received instead of being littered throughout the code base. The main
   message loop could also be made responsible for central
   error-handling/logging and sending of downlink messages when the processing
   of a request has completed. This would just require regular use of tasks.
   Next, all `Task`-based operations on `LoRaDevice` could be naturally and
   _implicitly_ queued and then executed in a mutually exclusive manner.

Another problem is that the joining of a device takes a separate execution
path than the uplink message handling and because the two are not
synchronized, there is room for race conditions between devices sharing the
same `DevAddr` but using different activation methods (OTAA and ABP). To
illustrate with an exampe, suppose an LNS has an empty device cache and a
device named A with a `DevAddr` of 1 (from a previous join) using OTAA
re-joins. Since the device is not in the cache, the LNS issues a request to
the server function to search for the device on IoT Hub and obtain its primary
key that's needed to open a device connection. Next, a `LoRaDevice` is
_minimally initialzed_ for the purpose of loading its twin and the twin data
requested over the device conneciton. During this time, suppose that another
device named B sharing the same `DevAddr` as device A but using ABP activation
sends an uplink message to the LNS. Since the `DevAddr` is not in the cache,
the LNS will issues a search request to the server function to return all
devices using the `DevAddr` in question.

Another problem is the initialization of `LoRaDevice`, which is
a two-step process: a _minimally initialized_ instance gets created first,
then its twin information is grabbed to make it fully ready for use by future
requests. At that point, the `LoRaDevice` instance is put into the
`LoRaDeviceCache` so that other requests can find it. While the twin
information is being fetched, there is a small window during which another
request could initiate the same process.

, and when it is announced in a
`LoRaDeviceCache`. This particularly The delay between the two can mean that .

## Decision

Use the approach to simplify the entire flow and processing of a message into
a simple request-response model. A quick spike demonstrated that the changes
to the main code base would be fairly contained and the largest impact is
expected to be in adapting the tests (which could also be done with a stop-gap
measure where the tests are adapted after the initial refactoring of the code
base).

All operations for a particular device requiring the IoT hub connection, are
requested to be executed on the `LoRaDevice` itself. The `LoRaDevice` becomes
a singleton. This will ensure, we only have one operation acting on the 
connection and allows us to have a deterministic way of adding a close 
operation without affecting any other operations currently being processed. 
The approach has a problem with the creation of the devices. 
A `LoRaDevice` requires the twins to be valid. That operation can't be 
synchronized on the `LoRaDevice` as that instance is technically not available.
Since it is not available, there should be no operation coming in, or those
should also trigger the load. A simple solution is to synchronize both
load operations in the `LoRaDeviceRegistry` and make sure there is only 
ever 1 operation loading the twins for a particular DevEui. 
We also concluded that closing the connection after initialization is the
easiest approach to delay the connection ownership decision to after the
first message has arrived.


Based on a spike of the refactoring this would require, it seems plausible to
achieve the refactoring of the actual code base within two weekly sprints. It
is expected that the bulk of the time will be spent in refactoring the test
code.

## Consequences

The chosen approach has the following benefits:

- Single use of `Task.Run` in `LnsProtocolMessageProcessor`. This reduces the
  number execution forks to consider as well as chances of exceptions going
  unobserved.

- Removes many abstractions like `ILoRaDeviceRequestQueue` and
  `IMessageDispatcher`, implementations like `DeviceLoaderSynchronizer` and
  `ExternalGatewayLoRaRequestQueue`, and potentially more.

- Easier to reason about the overall message flow.

- Use of the regular async-await programming model, including error-handling
  and cancellation that's built-in into tasks.

- Transparent use of queues through tasks in `LoRaDevice` to serialize
  execution. This subsumes the first approach (discussed in the introductory
  section) by having implicit rather explicit queuing (at the
  application-level).

- Greatest potential to simplify tests since assertions can rely on simply
  return values and exceptions.

- It is not mutually exclusive with other approaches explored. For example, by
  simplifying to the request-response model, it would be even easier to have
  the LNS operate with a single logical thread if that could further help
  remove some complexity (without compromising scalability) like locks.

The absence of an explicit queue could be make it more difficult for someone
to understand the code and choices made if they are not familiar with the
intricacies of how async-await operates.

[#1480]: https://github.com/Azure/iotedge-lorawan-starterkit/issues/1479
