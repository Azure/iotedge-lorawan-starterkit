# Azure IoT LoRaWan Starter Kit

This project is aimed at providing an easy way to connect LoRa sensors/gateways to the Azure Cloud.

**Project Leads:** [Ronnie Saurenmann](mailto://ronnies@microsoft.com) and 
[Todd Holmquist-Sutherland](mailto://toddhs@microsoft.com).

Reference implementation of LoRaWAN components to connect LoRaWAN antenna gateway running IoT Edge directly with Azure.

# Quickstart

A deployment template is available to deploy all the required Azure infrastructure and get you started quickly. Just press on the "Deploy to Azure button" here below. 
If you'd rather deploy it manually please jump directly into the [do it yourself section](/LoRaEngine).

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FSkraelinger%2FAzureIoT_LoRaWan_StarterKit%2Fmaster%2FTemplate%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

You will be ask to fill the following fields :
* **Resource Group** - A logical "folder" where all the template resource would be put into, just choose a meaningful name.
* **Location** -  In which Datacenter the resources should be deployed.
* **Unique Solution Prefix** - A string that would be used as prefix for all the resources name to ensure their uniqueness. Hence, avoid any standard prefix such as "lora" as it might already be in use and might make your deployment fail.
* **Edge gateway name** - the name of your LoRa Gateway node in the IoT Hub.
* **Deploy Device** - Do you want a demo end device to be already provisioned. If yes the code located in the [Arduino folder](/Arduino) would be ready to use immediately.

The deployment would take c.a. 10 minutes to complete, in the meanwhile please proceed to [install IoT Edge to your gateway](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-install-iot-edge-linux-arm).

Once it's done, please head up to the IoT Hub deployed in your template and go to IoT Edge Devices -> [your device name] then copy the connection string. 

Now SSH into your gateway, if your gateway is a Raspberry Pi, don't forget to [enable SPI](https://www.makeuseof.com/tag/enable-spi-i2c-raspberry-pi/) , (You need to restart your pi).

By using the ```docker ps``` command, you should see the Edge containers being deployed on your local gateway. You can now try one of the samples in the [Arduino folder](/Arduino) to see LoRa messages being sent to the cloud. 

# Customize the solution & Deep dive
Have a look at the [LoRaEngine folder](/LoRaEngine) for more in details explanation.

# Directory Structure
The solution is organized into different sections:
* **Arduino** - Sample codes to get started quickly with some Arduino samples and LoRa.
* **Device** - C/C++ implementations of device-specific LoRa packet forwarders.
* **EdgeVisualization** -a tool for visualizing packet flows in IoTEdge.
* **LoRaEngine** - a .NET Standard 2.0 solution with the following projects:
  * **LoRaDevTools** - a submodule containing useful tools to develop with LoRa
  * **LoRaWanTest** - a project testing the LoRa Library.
  * **LoRaKeysManagerFacade** - an Azure Function enabling OTAA Authentication and template deployment
  * **Modules** - contains code to build the modules deployed on the IoT edge gateway.
  * **PacketForwarderHost** - executable and IoTEdge configuration files.
  * **UDPListener** - executable
  * **LoRaTools** - library
  * **LoRaServer** - IoT edge module executable, Dockerfile, etc.
* **Template** - Contain code useful for the "deploy to Azure button"


