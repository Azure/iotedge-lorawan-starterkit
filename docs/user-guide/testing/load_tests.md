# Load Tests

The (simulated) load tests in `LoRaWan.Tests.Simulation` allow you to generate load on an arbitrary number of gateways by simulating an arbitrary number of concentrators. There are a number of load-test scenarios that are used to simulate real-world scenarios. Typically, you would want to use LNS components that are as close to a production scenario as possible (e.g. LNS deployed on VMs with the correct OS/SKUs in the cloud). You can then run these tests from your local machine to simulate many devices and many concentrators sending a large amount of messages to the gateways deployed in the cloud.

## Running the load tests

In `LoRaWan.Tests.Simulation` you have access to a set of load tests that can be used to validate the performance of the system. If you have access, you can either run the load tests from the E2E CI by setting the `RunLoadTests` input parameter to `true`. Alternatively, you can run the tests on your local machine. If you decide to run the tests on your local machine, first to copy the `appsettings.json` from the Simulator project into a `appsettings.local.json` and make sure that this file is copied over to the output directory. Replace the tokens in the `appsettings.local.json`:

- To connect the load tests with a locally running LNS, replace the value in `LoadTestLnsEndpointsString` with something similar to `{"LoadTestLnsEndpointsString":"[\"ws://<lns-url>:5000\"]"}`. You can insert an arbitrary number of LNS stations into the array, and the load tests will connect stations to LNS using round-robin distribution.
- The `DevicePrefix` can be any prefix that is used for the load test devices. If `CreateDevices` is set to `true`, it will create devices with this prefix in the IoT Hub that is referenced in your `appsettings.local.json`.
- Make sure that the `LeafDeviceGatewayID` matches the ID of one of the LNS that you connect to the load tests.

