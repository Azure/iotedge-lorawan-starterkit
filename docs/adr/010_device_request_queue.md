# Device Request Queue

- **Feature**: [#1480]
- **Author(s)**: Atif Aziz
- **Status**: Proposed

## Context

The LNS must maintain a single connection per device. There are a number of
complex and concurrent code paths in the current solution that make it hard to
reason about when a device connection is in use, when it's safe to open/close
without affecting another operation in flight and how various device-related
operations (such as refreshing from the twin) can work in isolation,
deterministically and without race conditions. If a single queue could be
maintained for all device-related operations then it would become easier to
order those operations and reason about them.

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

## Decision

Use the approach to simplify the entire flow and processing of a message into
a simple request-response model. A quick spike demonstrated that the changes
to the main code base would be fairly contained and the largest impact is
expected to be in adapting the tests (which could also be done with a stop-gap
measure where the tests are adapted after the initial refactoring of the code
base).

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
