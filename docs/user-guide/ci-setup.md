---
title: Continuous Integration Setup
---

LoRaWAN Starter Kit uses GitHub Actions for executing CI workflows. This page
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
the `LoRaWan.Tests.E2E` project). The pipeline runs tests using real LoRaWan hardware,
it therefore requires a VPN tunnel between the local hardware deployment and Azure. you
can find more information [on this blog post](https://devblogs.microsoft.com/cse/2022/03/15/e2e-tests-for-lorawan-starter-kit-with-real-hardware/).

The pipeline runs the following tasks:

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


### Pipeline authentication setup

The pipeline uses the Github environment named *CI* and [OIDC tokens](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/about-security-hardening-with-openid-connect) to authenticate to Azure. OIDC auth is enabled ONLY for this environement. All pipeline secrets are taken from a
Keyvault. The only environment secrets needed at the time of writing are :

- AZURE_CLIENT_ID : Required to set up the OIDC connection
- AZURE_TENANT_ID : Required to set up the OIDC connection
- AZURE_SUBSCRIPTION_ID : Required to set up the OIDC connection
- AZURE_FUNCTIONAPP_PUBLISH_PROFILE : Required as the [Azure Function step doesn't support OIDC auth](https://github.com/Azure/functions-action/issues/153)
- KEYVAULT_NAME : Required to indicate which keyvault the pipeline should point to

The OIDC connection is made using a service principal which has the following permissions:

- Desktop Virtualization Power On Off Contributor on the Eflow vm
- Contributor on the subscription
- Key Vault Secrets User on the Keyvault 

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

### Connecting to the Environment

CI operators can use an Azure VPN to [connect to the local CI using P2S](https://learn.microsoft.com/azure/vpn-gateway/vpn-gateway-howto-point-to-site-resource-manager-portal#connect), certificates and secrets are in the Keyvault

### Manual steps to recreate the environment

In addition to running the Bicep scripts, the following action needs to be manually executed:

1. Create a service principal with permissions as described [above](#pipeline-authentication-setup)
1. Create a new Github Environment (Or update the existing one) to reflect the client id of the new service principal
1. Follow the [documentation](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/configuring-openid-connect-in-azure) to create trust between Azure and the Github Environment
1. Create an Azure Keyvault and Migrate the secrets from the previous Keyvault
1. Create an Azure VPN Gateway
    - Create a certificate chain to authenticate following [this doc](https://learn.microsoft.com/azure/vpn-gateway/vpn-gateway-howto-point-to-site-resource-manager-portal)
    - Update the local CI Devices with the new certificate
    - Save the certificates and Key to the new Keyvault
1. Create a Windows Server 2022 VM to Host the Eflow VM
    - Installation instructions can be found [here](https://github.com/Azure/iotedge-lorawan-starterkit/tree/dev/Tools/Eflow/)
1. Create a new Azure Container Registry and import the required docker image (Convenience script [below](#docker-import-convenience-script)). Alternatively, rebuild it from [the sample folder](https://github.com/Azure/iotedge-lorawan-starterkit/tree/dev/Samples/DecoderSample).
1. Grant permissions to the newly created service principal to the new Azure resources as described [previously](#pipeline-authentication-setup)

#### Docker import convenience script

```powershell
$newRegistry=<your registry>
$previousRegistry=<the previous azure registry>
$previousRegistryLogin=<previous registry login>
$previousRegistryPW=<previous registry password>
$debianRelease=bullseye
az acr import --name $newRegistry --source docker.io/amd64/debian:$debianRelease --image amd64/debian:$debianRelease
az acr import --name $newRegistry --source docker.io/amd64/debian:$debianRelease-slim --image amd64/debian:$debianRelease-slim
az acr import --name $newRegistry --source docker.io/arm32v7/debian:$debianRelease --image arm32v7/debian:$debianRelease
az acr import --name $newRegistry --source docker.io/arm32v7/debian:$debianRelease-slim --image arm32v7/debian:$debianRelease-slim
az acr import --name $newRegistry --source docker.io/arm64v8/debian:$debianRelease --image arm64v8/debian:$debianRelease
az acr import --name $newRegistry --source docker.io/arm64v8/debian:$debianRelease-slim --image arm64v8/debian:$debianRelease-slim
az acr import --name $newRegistry --source docker.io/amd64/node:$debianRelease --image amd64/node:$debianRelease
az acr import --name $newRegistry --source docker.io/amd64/node:$debianRelease-slim --image amd64/node:$debianRelease-slim
az acr import --name $newRegistry --source docker.io/arm32v7/node:$debianRelease --image arm32v7/node:$debianRelease
az acr import --name $newRegistry --source docker.io/arm32v7/node:$debianRelease-slim --image arm32v7/node:$debianRelease-slim
az acr import --name $newRegistry --source docker.io/arm64v8/node:$debianRelease --image arm64v8/node:$debianRelease
az acr import --name $newRegistry --source docker.io/arm64v8/node:$debianRelease-slim --image arm64v8/node:$debianRelease-slim
az acr import --name $newRegistry --source $previousRegistry.azurecr.io/decodersample:2.0-arm32v7 --image decodersample:2.0-arm32v7 --username $previousRegistryLogin --password $previousRegistryPW
az acr import --name $newRegistry --source $previousRegistry.azurecr.io/decodersample:2.0-amd64 --image decodersample:2.0-amd64 --username $previousRegistryLogin --password $previousRegistryPW
az acr import --name $newRegistry --source $previousRegistry.azurecr.io/decodersample:2.0 --image decodersample:2.0 --username $previousRegistryLogin --password $previousRegistryPW
```

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
