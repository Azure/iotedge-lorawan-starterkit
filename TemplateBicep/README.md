# Deploying infrastructure using Bicep

This folder contains the Bicep files to deploy the Starter-Kit infrastructure.

This guide shows how to manually deploy it.

## Prerequisites

Make sure to have the following tool installed:

* [AZ CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)

## Deploying the infrastructure

Step 1: Login to Azure using the AZ CLI:

```plain
az login --tenant <tenant-id>
```

Step 2: Create target resource group

```plain
az group create --location <location> --resource-group <resource-group-name>
```

Step 3: Start deployment

```plain
az deployment group create --resource-group <resource-group-name> --template-file ./main.bicep --parameters uniqueSolutionPrefix="<unique-name>" resetPin=<based-on-your-setup> deployDevice=true edgeGatewayName="<gateway-device-name>" useDiscoveryService=true
```

## Developing

### Device creation

For device creation debugging, there are alternatives other than deploying the whole solution:

Option 1: Run bash script locally

```plain
FACADE_SERVER_URL="https://myapp.com/api" IOTHUB_CONNECTION_STRING="<iothub-connection-string>" LORA_CLI_URL="https://github.com/Azure/iotedge-lorawan-starterkit/releases/download/v<current-version>/lora-cli.linux-x64.tar.gz" EDGE_GATEWAY_NAME="<iotedge-device-name>" STATION_DEVICE_NAME="<concentrator-device-name>" DEPLOY_DEVICE=1 RESET_PIN=<concentrator-reset-pin> LORA_VERSION="<lora-starter-kit-release-version>" ./create_device.sh
```

Option 2: Run the device provisioning Bicep

```plain
az deployment group create --resource-group <resource-group-name> --template-file ./devices.bicep --parameters iothubName="<unique-name>" 
resetPin=<based-on-your-setup> edgeGatewayName="<gateway-device-name>" spiSpeed=<based-on-your-setup> spiDev=<based-on-your-setup> functionAppName="<function-name>" region="<lora-region>" stationEui="<concentrator-device-name>" logAnalyticsName="<log-analytics-name>" 
loraCliUrl="<lora-cli-linux-musl-version-download-url>" version="<lora-starter-kit-release-version>" deployDevice=true
```
