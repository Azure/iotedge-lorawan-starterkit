# CI setup

LoRaWAN StarterKit uses GitHub Actions for executing CI workflows. This page
describes the CI workflows which are currently used in the project.

## LoRa Build & Test CI

The [Build & Test CI pipeline][build-and-test-ci] runs the following tasks:

- Builds the `LoRaEngine` and `DecoderSample`
- Runs unit tests
- Runs integration tests
- Publishes test results
- Builds a docker image of the `LoRaEngine`

## LoRa E2E CI

The [E2E CI pipeline][e2e-ci] is used to run end-to-end tests (implemented in
the `LoRaWan.Tests.E2E` project). The pipeline runs the following tasks:

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

### Pipeline settings

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

### Execution

The E2E CI can be triggered manually via GitHub Actions and runs automatically
on a daily schedule. The pipeline is also set up to run on pull requests under
certain conditions.

In order for this workflow to be run on an open pull request, the label `fullci`
needs to be added to the PR. Upon successful execution of the tests, additional
labels will be added to the PR automatically by GitHub Actions, e.g. if all
multi-concentrator E2E tests pass, the `MultiConcentrator` label will
automatically be added to the PR.

The workflow will be re-executed on new changes being pushed, as long as the
`fullci` label is present. By default all E2E tests will be run each time
(including the ones which previously passed). In order to prevent this and only
run previously failing tests, additional label must be added to the PR (e.g.
label `1`). In this case the test labels already added to the PR will prevent
those tests from being re-executed.

## Universal Decoder CI

The [Universal decoder CI pipeline][decoder-ci] runs the following tasks:

- Builds and tests the Universal Decoder sample project
- Builds and pushes a docker image for the decoder

## Other workflows

The repository contains other workflows which are run automatically under
certain confitions, typically when a pull request is created:

- [CodeQL][codeql] - runs CodeQL analysis on each PR created against `dev`
  branch and after merge
- [Lint and Check Markdown][lint-markdown] - runs linter and checks links in
  markdown files; runs on PR created agains `docs/main` branch and only checks
  `.md` (or `.markdown`) files
- [Test Report][test-report] - publishes test results after Build & Test CI or
  E2E CI have completed
- [Publish docs (new version)][publish-docs-new-version] - publishes project
  documentation using MkDocs. The workflow needs to be triggered manually
  against `docs/main` branch after documentation has been updated and requires a
  version to publish as input to run.
- [Publish docs dev][publish-docs-dev] - runs on changes to `docs/main` branch
  and publishes `dev` version of the documentation

[build-and-test-ci]:
https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/.github/workflows/ci.yaml
[e2e-ci]:
https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/.github/workflows/e2e-ci.yaml
[decoder-ci]:
https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/.github/workflows/universal_decoder_ci.yaml
[codeql]:
https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/.github/workflows/codeql-analysis.yml
[lint-markdown]:
https://github.com/Azure/iotedge-lorawan-starterkit/blob/442391dbdfe110e09e8448db7e9098de28403f34/.github/workflows/md-linter.yaml
[test-report]:
https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/.github/workflows/test_report.yaml
[publish-docs-new-version]:
https://github.com/Azure/iotedge-lorawan-starterkit/blob/docs/main/.github/workflows/publish-docs-new-version.yml
[publish-docs-dev]:
https://github.com/Azure/iotedge-lorawan-starterkit/blob/docs/main/.github/workflows/publish-docs-dev.yml
