# LNS Migration plan

Date: **2021-10-01**

## Status

- Draft

## Context

We want to define a plan to build a basic support for the LoRa Basic Station in our LoRa Network Server. A subset of the team did a PoC contained into the branch named "lbs", but we decided to drop the branch and rebuild/rearchitect. The current document defines a plan on how to perform a clean migration in a defined amount of time.

> The present document is expected to provide a high level guidance on the minimal sequence of events needed to perform the migration in a clean but fast way. During the work, we might realize additional intermediate steps are needed before proceeding. In that case, this document will be edited with the additional steps with intermediate steps labeled as phase [1-9].[a-z].

## Out of scope

- Region refactoring will be tackled in another document / Stream
- Refactoring of the testing strategy will be tackled in another document.
- Class C refactoring and support will be tackled in another document.

## Plan

### Phase I

- The UDPServer class will be deprecated and planned to be deleted in the near future (Phase V).
- A new communication abstraction will be created from the [Program.cs](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWanNetworkSrvModule/Program.cs).
- An implementation of the communication abstraction will injected to support the communication with the LBS.
- The implementation instantiate an ASP.Net solution with support for routes.
- The tests for the implementation are created in a new test project 'UnitTests' that will hosting all project's unit tests.
- End to end test will not run until the Phase 5 is completed.
- A LNS Protocol Primitives (ID6, EUI, INT) is documented and implemented. (This [implementation](https://github.com/lorabasics/basicstation/blob/master/src/rt.h) could help as reference)
- Cleanup of existing code.

### Phase II

- The LBS Communication implementation implements a Websocket server.
  - Some research is needed around the best practices around multi-socket communication
- The Websocket endpoint implements ([spike code](https://github.com/Azure/iotedge-lorawan-starterkit/blob/lbs/LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWan.NetworkServer/BasicStation/WebSocketServer/LnsController.cs) might serve as example):
  - a discovery endpoint, accepting all connection for now (to be changed in the future)
  - a configuration endpoint, where data is hardcoded similarly to the LBS branch
- Unit tests are in the unit test project

### Phase III

- The Websocket endpoint should be able to receive upstream messages and send it to the existing [message dispatcher](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWan.NetworkServer/MessageDispatcher.cs).
  - Classes should be changed to remove the RXPK reference. (e.g. [LoRaRequest](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWan.NetworkServer/LoRaRequest.cs) )
- The Existing DefaultMessageHandler and JoinMessageHandler will be changed to use LBS primitives. Downlink message is not in scope.
  - Class C messages seems embedded into the defaultmessage sender. Sync with Francisco / Ronnie to understand why.

### ADR Interlude I

- Websocket Connection Dictionary ADR

### Phase IV

- The Websocket connection Dictionary need to be implemented
- Downlink message are added to DefaultMessageHandler and JoinMessageHandler

### ADR Interlude II

- ADR document on Testing strategy.

### Phase V

- E2E tests are renabled excepting Class C. We debug them.
- Refactor of existing tests (LoRaWanNetworkSrv.Test) to use LBS class and transfer to projects according to testing sttategy design.

## Subsequent work

- Class C enablement
- Region integration
- Separation between the message processing and validation
- Multi LBS Support

## Decision

Adopt the [migration plan](#plan)

## Consequences

Once the ADR approved, we should create associated work item.
