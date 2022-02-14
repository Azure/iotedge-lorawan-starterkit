# ModuleClient for upstream telemetry as an alternative to DeviceClient

## Challenge

In LoRaWAN Starter Kit, each sensor has its own identity in IoT Hub device registry. This allows kit users to use such identities for both sending telemetry messages and storing a state in device twin for LoRa specific properties (i.e.: session keys, frame counter, dwell time settings et cetera).

During load tests it was noticed that, if a device is in reach of multiple LoRaWAN Network Servers, the related edgeHub modules are fighting each other for acquiring and keeping the connection open for all the operations mentioned above.

When trying to handle approximately more than one hundred of leaf devices like this, a slow-down of the solution was noticed, in addition with some dropped upstream messages.

We think it is very critical to not drop any upstream message, therefore we should try to find some alternatives.

## Approach

The approach being considered in this document is still making use of `Microsoft.Azure.Devices.Client.DeviceClient` for twin operations and C2D messages only, while routing telemetry data through the LoRaWAN Network Server `Microsoft.Azure.Devices.Client.ModuleClient`.

This idea is trying to reduce dramatically the amount of links needed for sending telemetry upstream, by enqueueing telemetry messages on the LNS IoT Edge module outputs.

## Load test result

`Connected_Factory_Load_Test_Scenario` was chosen as test for understanding the behaviour of the proposed approach.

After modifying the 2.0.0 code base with the required changes, the test bench was setup with 1500 leaf devices, running in 2 factories, with 1 LNS per factory and 2 concentrators per LNS. IoT Hub was sized to 1x S3 unit, in order to not be throttled on twin operations.

While executing the test, we were able to observe that no upstream message was dropped. Nevertheless, the "connection ping-pong" caused other issues like delays in twin updates, causing some delays/retries for OTAA join requests, and inability of sending acknowledgment on time for the expected receive windows (20% of the acknowledgements was dropped).

## Conclusions

### Advantages

- This approach is fairly easy to implement and requires very small change in current code base
- Using a ModuleClient is allowing to have a higher guarantee of delivering the telemetry messages
- The connection ping-pong of DeviceClients, is partially mitigated. Even though the ping-pong is still there, this is not impacting the crucial telemetry operations

### Disadvantages

- The "identity" of the device which generated the message is lost. In IoT Hub, messages would be received from the LNS IoT Edge module of the IoT Edge gateway where the solution is running. While it's theoretically still possible to identify the originating device (i.e. by setting a message property), this would be a breaking change requiring a substantial change in all the applications currently consuming the data in IoT Hub.
- This solution, per-se, is not enough to completely solve the connection ping-pong issue, therefore additional measures should be taken (i.e. LNS affinity)
