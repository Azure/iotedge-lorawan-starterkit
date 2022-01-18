# Integration Tests

Coming soon

## Running the load tests

In `LoRaWan.Tests.Simulation` you have access to a set of load tests that can be used to validate the performance of the system. You can either run the load tests from the CI by setting the `RunLoadTests` input parameter to `true`, or you can run it on your local machine. If you decide to run it on your local machine, make sure to copy the `appsettings.json` from the Simulator project into a `appsettings.local.json` and make sure that this file is copied over to the build output directory. Replace the tokens in the `appsettings.local.json`:

- To connect the load tests with a locally running LNS, replace the value in `LoadTestLnsEndpointsString` with something similar to `{"LoadTestLnsEndpointsString":"[\"ws://<lns-url>:5000\"]"}`. You can insert an arbitrary number of LNS stations into the array, and the load tests will connect stations to LNS using round-robin distribution.
- The `DevicePrefix` can be any prefix that is used for the load test devices. If `CreateDevices` is set to `true`, it will create devices with this prefix in the IoT Hub that is referenced in your `appsettings.local.json`.
- Make sure that the `LeafDeviceGatewayID` matches the ID of one of the LNS that you connect to the load tests.

