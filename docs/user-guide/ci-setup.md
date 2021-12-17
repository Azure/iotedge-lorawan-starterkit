# CI setup

## CI pipelines

LoRaWAN StarterKit uses GitHub Actions for executing CI workflows. The
repository contains the following pipeline templates:

### LoRa Build & Test CI

The pipeline template can be found in `.github/workflows/ci.yaml`. The workflow
runs the following tasks:

- Builds the `LoRaEngine` and `DecoderSample`
- Runs unit tests
- Runs integration tests
- Publishes test results
- Builds a docker image of the `LoRaEngine`

### LoRa E2E CI

The E2E pipeline is used to run end-to-end tests (implemented in the
`LoRaWan.Tests.E2E` project). The template is located in
`.github/workflows/e2e-ci.yaml`. The pipeline runs the following tasks:

- Prepares the required environment variables
- Builds and deploys Facade Azure Function
- Builds and pushes docker images for `LoRaWanNetworkServer` and
  `LoRaWanBasicsStation` modules
- Generates required server and client certificates using scripts located in
  `Tools/BasicStation-Certificates-Generation/`; the trust certificates are then
  copied to a pre-created `$CERT_REMOTE_PATH` location in the local CI and to a
  remote device
- Deploys IoT Edge solution to ARM gateway
- Deploys IoT Edge solution to EFLOW gateway
- Deploys IoT Edge solution to a standalone concentrator
- Runs E2E tests on a dedicated agent

The E2E pipeline has a number of settings that can be configured in GitHub
Actions:

- `RunE2ETestsOnly` - if set to `true` only the E2E tests will be run, all other
  steps of the workflow will be skipped
- `E2ETestsToRun` - allows selection of only specific E2E tests to be run using
  the E2E test class name pattern, e.g. if set to `[MultiGatewayTest]` only E2E
  tests implemented in `Tests/E2E/MultiGatewayTests.cs` will be run. The
  variable defaults to all E2E tests.
- `TxPower` - allows setting a custom transmission power on the leaf devices to
  be used during the E2E test run (with 14 being the maximum). The default is TX
  power is 6.
