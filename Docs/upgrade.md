# Upgrade LoRaWAN to a new version

## Release 1.0.4
To update from version 1.0.3 you can follow the below instructions. If you want to update manually from a version prior to 1.0.3, please refer to the instructions in the [Release 1.0.3](#Release-1.0.3) section below.

### Updating from 1.0.3
|Deployment Module|Image URI|
|-|-|
|LoRaWanNetworkSrvModule|loraedge/lorawannetworksrvmodule:1.0.4|
|LoRaWanPktFwdModule|loraedge/lorawanpktfwdmodule:1.0.4|

On the same `Set Modules` page, also update your current edge version to 1.0.9.4 by pressing the `Configure Advanced Edge Runtime settings` button. On the menu, ensure the edge hub and edge agent are using version 1.0.9.4 by respectively setting image name to mcr.microsoft.com/azureiotedge-hub:1.0.9.4 and mcr.microsoft.com/azureiotedge-agent:1.0.9.4.

### Updating the Azure Function Facade

If you have manually deployed the Azure Function, re-deploy the updated version of the Azure Function Facade as outlined [here](./devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

If you have deployed the solution and with it the Azure Function through the Azure Resource Manager template, you will see an `App Setting` in the function with the name "WEBSITE_RUN_FROM_ZIP". Update it's value to:

```
https://github.com/Azure/iotedge-lorawan-starterkit/releases/download/v1.0.4/function-1.0.4.zip
```

## Release 1.0.3

To update from 1.0.1 or 1.0.2 you can follow the below instructions. If you want to update manually from a version prior to 1.0.1, please refer to [Updating existing installations from 1.0.0 to release 1.0.1](##Updating-existing-installations-from-1.0.0-to-release-1.0.1) section below.

### Updating existing installations from 1.0.1 or 1.0.2 to release 1.0.3

Go to your solution's Azure IoT Hub and under IoT Edge, select each of your gateways. Select `Set Modules` and configure the two deployment modules `LoRaWanNetworkSrvModule` and `LoRaWanPktFwdModule`. Make sure, the following image URIs are configured:

|Deployment Module|Image URI|
|-|-|
|LoRaWanNetworkSrvModule|loraedge/lorawannetworksrvmodule:1.0.3|
|LoRaWanPktFwdModule|loraedge/lorawanpktfwdmodule:1.0.3|

On the same `Set Modules` page, also update your current edge version to 1.0.9 by pressing the `Configure Advanced Edge Runtime settings` button. On the menu, ensure the edge hub and edge agent are using version 1.0.9 by respectively setting image name to mcr.microsoft.com/azureiotedge-hub:1.0.9 and mcr.microsoft.com/azureiotedge-agent:1.0.9.

### Updating the Azure Function Facade

If you have manually deployed the Azure Function, re-deploy the updated version of the Azure Function Facade as outlined [here](./devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

If you have deployed the solution and with it the Azure Function through the Azure Resource Manager template, you will see an `App Setting` in the function with the name "WEBSITE_RUN_FROM_ZIP". Update it's value to:

```
https://github.com/Azure/iotedge-lorawan-starterkit/releases/download/v1.0.3/function-1.0.3.zip
```

## Release 1.0.2

We recommend re-deploying your solution based on the 1.0.2 release if you have been working with a solution before version 1.0.2. To update from 1.0.1 you can follow the below instructions. If you want to update manually from a version prior to 1.0.1, please refer to [Updating existing installations from 1.0.0 to release 1.0.1](##Updating-existing-installations-from-1.0.0-to-release-1.0.1) section below.

### Updating existing installations from 1.0.1 to release 1.0.2

Go to your solution's Azure IoT Hub and under IoT Edge, select each of your gateways. Select `Set Modules` and configure the two deployment modules `LoRaWanNetworkSrvModule` and `LoRaWanPktFwdModule`. Make sure, the following image URIs are configured:

|Deployment Module|Image URI|
|-|-|
|LoRaWanNetworkSrvModule|loraedge/lorawannetworksrvmodule:1.0.2|
|LoRaWanPktFwdModule|loraedge/lorawanpktfwdmodule:1.0.2|

On the same `Set Modules` page, also update your current edge version to 1.0.7 by pressing the `Configure Advanced Edge Runtime settings` button. On the menu, ensure the edge hub and edge agent are using version 1.0.7 by respectively setting image name to mcr.microsoft.com/azureiotedge-hub:1.0.7 and mcr.microsoft.com/azureiotedge-agent:1.0.7.

### Updating the Azure Function Facade

If you have manually deployed the Azure Function, re-deploy the updated version of the Azure Function Facade as outlined [here](./devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

If you have deployed the solution and with it the Azure Function through the Azure Resource Manager template, you will see an `App Setting` in the function with the name "WEBSITE_RUN_FROM_ZIP". Update it's value to:

```
https://github.com/Azure/iotedge-lorawan-starterkit/releases/download/v1.0.2/function-1.0.2.zip
```

## Release 1.0.1

We recommend re-deploying your solution based on the 1.0.1 release if you have been working with a pre-release version. If you prefer to update your existing installation, the following lists describes the required steps.

## Updating existing installations from 1.0.0 to release 1.0.1

### Updating your gateways' IoT Edge module versions

Go to your solution's Azure IoT Hub and under IoT Edge, select each of your gateways. Select `Set Modules` and configure the two deployment modules `LoRaWanNetworkSrvModule` and `LoRaWanPktFwdModule`. Make sure, the following image URIs are configured:

|Deployment Module|Image URI|
|-|-|
|LoRaWanNetworkSrvModule|loraedge/lorawannetworksrvmodule:1.0.1|
|LoRaWanPktFwdModule|loraedge/lorawanpktfwdmodule:1.0.1|

### Updating the Azure Function Facade

If you have manually deployed the Azure Function, re-deploy the updated version of the Azure Function Facade as outlined [here](./devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

If you have deployed the solution and with it the Azure Function through the Azure Resource Manager template, you will see an `App Setting` in the function with the name "WEBSITE_RUN_FROM_ZIP". Update it's value to:

```
https://github.com/Azure/iotedge-lorawan-starterkit/releases/download/v1.0.1/function-1.0.1.zip
```

Edit the Function's `Application settings` and make sure the `FUNCTIONS_EXTENSION_VERSION` App setting is set to `~2`.
If during a previous upgrade you manually set it to `2.0.12342.0`, please change it back to `~2`.

|App Settings Name|Value|
|-|-|
|FUNCTIONS_EXTENSION_VERSION|**~2**|

Make sure the IoT Hub and Redis connection strings are properly configured in the function.

## Updating existing installations from 0.4.0-preview to release 1.0.0

### Updating the Azure Function Facade

If you have manually deployed the Azure Function, re-deploy the updated version of the Azure Function Facade as outlined [here](./devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

If you have deployed the solution and with it the Azure Function through the Azure Resource Manager template, you will see an `App Setting` in the function with the name "WEBSITE_RUN_FROM_ZIP". Update it's value to:

```
https://github.com/Azure/iotedge-lorawan-starterkit/releases/download/v1.0.0/function-1.0.0.zip
```

Edit the Function's `Application settings` and change the `FUNCTIONS_EXTENSION_VERSION` App setting from `~2` to `2.0.12342.0`

|App Settings Name|Value|
|-|-|
|FUNCTIONS_EXTENSION_VERSION|**2.0.12342.0**|

Make sure the IoT Hub and Redis connection strings are properly configured in the function.

## Updating existing installations from 0.3.0-preview to 0.4.0-preview

### Updating IoT Edge Runtime Containers to Version 1.0.6 ###

We highly recommend running the latest version of the IoT Edge runtime containers on your gateway to Version 1.0.6. The way that you update the `IoT Edge agent` and `IoT Edge hub` containers depends on whether you use rolling tags (like 1.0) or specific tags (like 1.0.2) in your deployment. 

The process is outlined in detail [here](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-update-iot-edge#update-the-runtime-containers).

Furthermore, make sure, the following environment variables are set for your `Edge hub` container:

```
mqttSettings__enabled: false
httpSettings__enabled: false
TwinManagerVersion: v2
```

You do this by clicking "Set Modules" &rarr; "Configure advanced edge runtime settings" on your IoT Edge device in Azure IoT Hub.

Make sure the **DevAddr** of your ABP LoRa devices starts with **"02"**: Due to addition of NetId support in this pre-relese, ABP devices created by the template  prior to 0.4.0-preview (and all devices with an incompatible NetId in general) will be incompatible with the 0.4.0-preview. In this case, make sure the DevAddr of your ABP LoRa devices starts with "02".

### Updating the Azure Function Facade

Re-deploy the updated version of the Azure Function Facade as outlined [here](./devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

Make sure the IoT Hub and Redis connection strings are properly configured in the function.

## Updating existing installations from 0.2.0-preview to 0.3.0-preview

### Updating IoT Edge Runtime Containers to Version 1.0.5

We highly recommend running the latest version of the IoT Edge runtime containers on your gateway (Version 1.0.5 at the time of writing). The way that you update the `IoT Edge agent` and `IoT Edge hub` containers depends on whether you use rolling tags (like 1.0) or specific tags (like 1.0.2) in your deployment. 

The process is outlined in detail [here](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-update-iot-edge#update-the-runtime-containers).

### Updating the Azure Function Facade

Re-deploy the updated version of the Azure Function Facade as outlined [here](./devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

Make sure the IoT Hub and Redis connection strings are properly configured in the function.
