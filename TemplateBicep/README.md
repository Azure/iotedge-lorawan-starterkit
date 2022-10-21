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
