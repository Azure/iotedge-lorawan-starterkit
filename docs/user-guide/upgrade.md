# Upgrade LoRaWAN to a new version
<!-- allow github to generate 'copy' buttons for coded URLs -->
<!-- markdownlint-disable MD040 -->

## Release 2.0.0-alpha (not released yet)

With release 2.0.0 we will use .NET 6 for the starter kit. Our docker images will be based on Debian 11 (Bullseye). Please make sure that if you plan to use our Docker images, you use a Debian 11-based OS instead of Debian 10. If you plan to use a Debian 10-based OS, you will need to build a custom Docker image.

### Upgrading to Raspberry Pi OS (bullseye) 

If you are running IoT Edge on a Raspberry Pi that is based on Debian 10 (Buster), we recommend that you download a new version of the image and perform a clean install.

After you installed the latest version of Raspberry Pi OS, execute the following commands to install IoT Edge:

```bash
curl https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb > ./packages-microsoft-prod.deb
sudo apt-get install ./packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install moby-engine
curl -L https://github.com/Azure/azure-iotedge/releases/download/1.2.5/aziot-identity-service_1.2.4-1_debian11_armhf.deb -o aziot-identity-service.deb && sudo apt-get install ./aziot-identity-service.deb
curl -L https://github.com/Azure/azure-iotedge/releases/download/1.2.5/aziot-edge_1.2.5-1_debian11_armhf.deb -o aziot-edge.deb && sudo apt-get install ./aziot-edge.deb
```

### Azure Functions

To support .NET 6 you will need to upgrade the Azure Functions runtime to v4. To update, you can set `FUNCTIONS_EXTENSION_VERSION` to `~4` in your Function configuration.

## Release 1.0.7

To update from version 1.0.6, 1.0.5, 1.0.4 or 1.0.3 you can follow the below instructions. If you want to update manually from a version prior to 1.0.3, please refer to the instructions in the [Release 1.0.3](#Release-1.0.3) section below.

### Update the IoT Edge security daemon when upgrading from IoT Edge 1.1 (release prior to 1.0.6)

Since release 1.0.6, the starter kit uses Azure IoT Edge version 1.2 which includes major changes the the IoT Edge Security daemon. Please follow this documentation to [Update IoT Edge](https://docs.microsoft.com/azure/iot-edge/how-to-update-iot-edge?view=iotedge-2020-11&tabs=linux) to upgrade Azure IoT Edge to 1.2.

### Updating from release post 1.0.3

|Deployment Module|Image URI|
|-|-|
|LoRaWanNetworkSrvModule|loraedge/lorawannetworksrvmodule:1.0.7|
|LoRaWanPktFwdModule|loraedge/lorawanpktfwdmodule:1.0.7|

On the same `Set Modules` page, also update your current edge version to 1.2.2 by pressing the `Configure Advanced Edge Runtime settings` button. On the menu, ensure the edge hub and edge agent are using version 1.2.2 by respectively setting image name to mcr.microsoft.com/azureiotedge-hub:1.2.2 and mcr.microsoft.com/azureiotedge-agent:1.2.2.

### Updating the Azure Function Facade

If you are upgrading from release 1.0.5, There are no changes on the Azure function therefore you can use the same bin. If you are upgrading from a previous release please follow function deployment guidance under release [Release 1.0.5](#Release-1.0.5).

## Release 1.0.6

To update from version 1.0.5, 1.0.4 or 1.0.3 you can follow the below instructions. If you want to update manually from a version prior to 1.0.3, please refer to the instructions in the [Release 1.0.3](#Release-1.0.3) section below.

### Update the IoT Edge security daemon when upgrading from IoT Edge 1.1

Since release 1.0.6, the starter kit uses Azure IoT Edge version 1.2 which includes major changes the the IoT Edge Security daemon. Please follow this documentation to [Update IoT Edge](https://docs.microsoft.com/azure/iot-edge/how-to-update-iot-edge?view=iotedge-2020-11&tabs=linux) to upgrade Azure IoT Edge to 1.2.

### Updating from 1.0.5, 1.0.4 or 1.0.3

|Deployment Module|Image URI|
|-|-|
|LoRaWanNetworkSrvModule|loraedge/lorawannetworksrvmodule:1.0.6|
|LoRaWanPktFwdModule|loraedge/lorawanpktfwdmodule:1.0.6|

On the same `Set Modules` page, also update your current edge version to 1.2.2 by pressing the `Configure Advanced Edge Runtime settings` button. On the menu, ensure the edge hub and edge agent are using version 1.2.2 by respectively setting image name to mcr.microsoft.com/azureiotedge-hub:1.2.2 and mcr.microsoft.com/azureiotedge-agent:1.2.2.

### Updating the Azure Function Facade

There are no changes on the Azure function therefore you can use the same versioning as Release 1.0.5 just below.

## Release 1.0.5

To update from version 1.0.4 or 1.0.3 you can follow the below instructions. If you want to update manually from a version prior to 1.0.3, please refer to the instructions in the [Release 1.0.3](#Release-1.0.3) section below.

### Updating from 1.0.4 or 1.0.3

|Deployment Module|Image URI|
|-|-|
|LoRaWanNetworkSrvModule|loraedge/lorawannetworksrvmodule:1.0.5|
|LoRaWanPktFwdModule|loraedge/lorawanpktfwdmodule:1.0.5|

On the same `Set Modules` page, also update your current edge version to 1.0.9.5 by pressing the `Configure Advanced Edge Runtime settings` button. On the menu, ensure the edge hub and edge agent are using version 1.0.9.5 by respectively setting image name to mcr.microsoft.com/azureiotedge-hub:1.0.9.5 and mcr.microsoft.com/azureiotedge-agent:1.0.9.5.

### Updating the Azure Function Facade

If you have manually deployed the Azure Function, re-deploy the updated version of the Azure Function Facade as outlined [here](devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

If you have deployed the solution and with it the Azure Function through the Azure Resource Manager template, you will see an `App Setting` in the function with the name "WEBSITE_RUN_FROM_ZIP". Update it's value to:

```
https://github.com/Azure/iotedge-lorawan-starterkit/releases/download/v1.0.5/function-1.0.5.zip
```

## Release 1.0.4

To update from version 1.0.3 you can follow the below instructions. If you want to update manually from a version prior to 1.0.3, please refer to the instructions in the [Release 1.0.3](#Release-1.0.3) section below.

### Updating from 1.0.3

|Deployment Module|Image URI|
|-|-|
|LoRaWanNetworkSrvModule|loraedge/lorawannetworksrvmodule:1.0.4|
|LoRaWanPktFwdModule|loraedge/lorawanpktfwdmodule:1.0.4|

On the same `Set Modules` page, also update your current edge version to 1.0.9.4 by pressing the `Configure Advanced Edge Runtime settings` button. On the menu, ensure the edge hub and edge agent are using version 1.0.9.4 by respectively setting image name to mcr.microsoft.com/azureiotedge-hub:1.0.9.4 and mcr.microsoft.com/azureiotedge-agent:1.0.9.4.

### Updating the Azure Function Facade

If you have manually deployed the Azure Function, re-deploy the updated version of the Azure Function Facade as outlined [here](devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

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

If you have manually deployed the Azure Function, re-deploy the updated version of the Azure Function Facade as outlined [here](devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

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

If you have manually deployed the Azure Function, re-deploy the updated version of the Azure Function Facade as outlined [here](devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

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

If you have manually deployed the Azure Function, re-deploy the updated version of the Azure Function Facade as outlined [here](devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

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

If you have manually deployed the Azure Function, re-deploy the updated version of the Azure Function Facade as outlined [here](devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

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

### Updating IoT Edge Runtime Containers to Version 1.0.6

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

Re-deploy the updated version of the Azure Function Facade as outlined [here](devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

Make sure the IoT Hub and Redis connection strings are properly configured in the function.

## Updating existing installations from 0.2.0-preview to 0.3.0-preview

### Updating IoT Edge Runtime Containers to Version 1.0.5

We highly recommend running the latest version of the IoT Edge runtime containers on your gateway (Version 1.0.5 at the time of writing). The way that you update the `IoT Edge agent` and `IoT Edge hub` containers depends on whether you use rolling tags (like 1.0) or specific tags (like 1.0.2) in your deployment.

The process is outlined in detail [here](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-update-iot-edge#update-the-runtime-containers).

### Updating the Azure Function Facade

Re-deploy the updated version of the Azure Function Facade as outlined [here](devguide.md#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

Make sure the IoT Hub and Redis connection strings are properly configured in the function.

<!-- markdownlint-enable MD040 -->
