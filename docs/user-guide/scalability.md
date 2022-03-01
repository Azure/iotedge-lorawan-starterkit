# Scalability

Different [deployment scenarios](./deployment-scenarios.md) have impact
on the scalability of your LNS (LoRaWAN Network Server).

## Connection ownership

The LNS is running as a module on IoT edge and processes messages
from potentially multiple LBS (LoRa Basics Stations).

Due to the broadcasting of messages, the same message from the same
device can reach multiple LNS.

One of the limiting factor is the connection awareness of IoT edge for
its connected device clients. Edge keeps an active connection for all
devices connected to it. If we have multiple open connections on multiple
LNS servers for the same device identity, we experience an aggressive
connection open/close pattern seriously affecting scalability of the LNS.

Starting with version 2.1 we introduce a concept for connection ownership
ensuring we only keep one active connection / LNS most of the time.

To facilitate this, we track the last LNS that won the race to process
a particular message. This LNS is then given an edge for future message
processing for that device by a configurable amount. This allows
the owning gateway to keep the connection open and keep processing messages
without having to fight for the connection from other LNS's. LNS's that do not
own the connection, never open it (unless for occasional cache refreshes).

This allows us to have a high percentage of single connection management
towards IoT hub.

Should an owning LNS go down and a message for that device is sent,
another LNS will eventually win the race and take the ownership.
Guaranteeing seemless failover and message processing.

Ownership is tracked both locally on the LNS to determine the connection
state as well as on the function. The function side is used to send
a notification, in case of an ownership change, to the previously
owning LNS. This is covering the case of roaming devices, where a
device could move outside of the reach of the owning gateway. That means
it won't get any new messages and won't notice, the ownership change
unless it is being informed by the function.

## Deduplication

There is a feature built into the LNS to
[deduplicate messages](./../adr/007_message_deduplication.md), to
deal with duplicate messages on multiple LNS. This does
have an effect on scalability. Higher scalability as described above
can only be achieved with `None` and `Drop`. Any other settings
will require the connection to be opened during message processing
on multiple gateways and does not allow the ownership of a
connection on a single LNS.
