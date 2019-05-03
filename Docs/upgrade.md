# Upgrade LoRaWAN starter kit to a new version

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

Re-deploy the updated version of the Azure Function Facade as outlined [here](/LoRaEngine#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

Make sure the IoT Hub and Redis connection strings are properly configured in the function.

## Updating existing installations from 0.2.0-preview to 0.3.0-preview

### Updating IoT Edge Runtime Containers to Version 1.0.5 ###

We highly recommend running the latest version of the IoT Edge runtime containers on your gateway (Version 1.0.5 at the time of writing). The way that you update the `IoT Edge agent` and `IoT Edge hub` containers depends on whether you use rolling tags (like 1.0) or specific tags (like 1.0.2) in your deployment. 

The process is outlined in detail [here](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-update-iot-edge#update-the-runtime-containers).

### Updating the Azure Function Facade

Re-deploy the updated version of the Azure Function Facade as outlined [here](/LoRaEngine#setup-azure-function-facade-and-azure-container-registry) if you have a previous version of this Azure Function running.

Make sure the IoT Hub and Redis connection strings are properly configured in the function.
