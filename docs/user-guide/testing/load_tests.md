# Load Tests

The (simulated) load tests in `LoRaWan.Tests.Simulation` allow you to generate load on an arbitrary number of gateways by simulating an arbitrary number of concentrators. There are a number of load-test scenarios that are used to simulate real-world scenarios. Typically, you would want to use LNS components that are as close to a production scenario as possible (e.g. LNS deployed on VMs with the correct OS/SKUs in the cloud). You can then run these tests from your local machine to simulate many devices and many concentrators sending a large amount of messages to the gateways deployed in the cloud.

## Running the load tests

In `LoRaWan.Tests.Simulation` you have access to a set of load tests that can be used to validate the performance of the system. If you have access, you can either run the load tests from the E2E CI by setting the `RunLoadTests` input parameter to `true`. Alternatively, you can run the tests on your local machine. If you decide to run the tests on your local machine, first to copy the `appsettings.json` from the Simulator project into a `appsettings.local.json` and make sure that this file is copied over to the output directory. Replace the tokens in the `appsettings.local.json`:

- To connect the load tests with a locally running LNS, replace the value in `LoadTestLnsEndpointsString` with something similar to `{"LoadTestLnsEndpointsString":"[\"ws://<lns-url>:5000\"]"}`. You can insert an arbitrary number of LNS stations into the array, and the load tests will connect stations to LNS using round-robin distribution.
- The `DevicePrefix` can be any prefix that is used for the load test devices. If `CreateDevices` is set to `true`, it will create devices with this prefix in the IoT Hub that is referenced in your `appsettings.local.json`.
- Make sure that the `LeafDeviceGatewayID` matches the ID of one of the LNS that you connect to the load tests.

## Example load tests

**January 21st, 2022 load test of v2.0.0-beta1.**

This load test used the default deduplication strategy for the `Connected_Factory_Load_Test_Scenario` load test scenario. First, we deployed two LNS on Standard D2s v3 Debian 11 VMs (2vCPU, 8GB of memory). We simulated 1600 OTAA devices, distributed among eight factories with four concentrators each. All devices are in reach of all concentrators within the same factory and send five messages each, starting with a join rate of 1.5 messages per second (to not hit the IoT Hub S1 quota) and then with progressively sending messages faster and faster (starting at 1.5 messages per second to pre-warm the cache and not hit IoT Hub S1 quota, and then successively increase the load to around 9.5 messages per second). Keep in mind that the effective message rate is higher than 9.5 messages per second, since every message is delivered to the gateways by the four concentrators per factory. The following bugs/issues became apparent in the load test:

- [AMQP exceptions when running load tests · Issue #1348 · Azure/iotedge-lorawan-starterkit (github.com)](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1348)
- [AMQP exceptions when handling > 1000 devices · Issue #1337 · Azure/iotedge-lorawan-starterkit (github.com)](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1337)
- [We should send messages upstream when on DeduplicationStrategy Mark or None. · Issue #1032 · Azure/iotedge-lorawan-starterkit (github.com)](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1032)
- [Resubmit threshold does not consider deduplication strategy used · Issue #1334 · Azure/iotedge-lorawan-starterkit (github.com)](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1334)
- [Investigate memory evolution of LNS under load · Issue #1374 · Azure/iotedge-lorawan-starterkit (github.com)](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1374)

In addition to the issues, for which we will not provider more details here, we discuss several performance/health indicators.

**Unhandled exceptions**: For a total of ca. 10k messages sent, all exceptions that we saw (ca. 48) were caused by one of the issues mentioned above.

**D2CMessageDeliveryLatency**: The D2C message delivery latency took a distinct shape for the three phases: in the join phase, the average processing time was ca. 100ms, then for the first round of messages (cache pre-warming) the average delivery/response time was ca. 800ms on the gateway winning the race. As soon as the cache was warm, the response time dropped to ca. 450ms. Zero receive windows were missed for all ca. 10k messages.

![image-20220124131449660](..\..\images\lt-message-latency.png)

**Memory and CPU usage**: CPU usage was fairly stable. However, while memory was staying between 100 and 130MB for the LNS during the entire load test. To ensure that we do not have a memory leak, we ran a longer load test over the course of several hours, during which memory was bounded at ca. 200MB and analysis of the Heap Dump revealed that the largest contributor to the Gen 2 Heap were the device connections (as expected), which the LNS manages in an internal cache.

![host-stats](..\..\images\lt-host-stats.png)

**January 24th, 2022 load test of v2.0.0-beta1.** This load test was for deduplication strategy drop, the same parameters as the January 21st load test, except that we send seven messages per device, ending at 13.5 messages per second (giving a total of 12800 messages in one hour). The analysis was identical to the January 21st load test, there were no new findings and observations match everything we saw before.
