# CI for LoRaWAN StarterKit

## CI pipelines

LoRaWAN StarterKit uses GitHub Actions for executing CI workflows. The repository contains the following pipeline templates:

- LoRa Build & Test CI

The pipeline template can be found in `.github/workflows/ci.yaml`. The workflow builds the `LoRaEngine` and `DecoderSample` projects, runs unit and integration tests and builds a docker image of the `LoRaEngine`. 

- LoRa E2E CI

The E2E pipeline is used to run end-to-end tests (implemented in the `LoRaWan.Tests.E2E` project). 
