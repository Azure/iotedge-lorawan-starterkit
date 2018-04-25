# Azure IoT LoRaWan Starter Kit

**Project Leads:** [Ronnie Saurenmann](mailto://ronnies@microsoft.com) and 
[Todd Holmquist-Sutherland](mailto://toddhs@microsoft.com).

Reference implementation of LoRaWAN components to connect LoRaWAN antenna gateway running IoT Edge directly with Azure IoT.

This project is very much a work-in-progress. At this point it doesn't do much.

The goal of the project is to (TBD fill in the goal)

# Directory Structure
The code is organized into three sections:
* **LoRaEngine** - a .NET Standard 2.0 solution with the following projects:
  * **PacketForwarderHost** - executable and IoTEdge configuration files.
  * **UDPListener** - executable
  * **LoRaTools** - library
  * **LoRaServer** - IoT edge module executable, Dockerfile, etc.
  * DevTools - folder
    * **PacketForwarderSimulator** - executable
    * **DevTool1** - executable used during development process
    * **DevTool2** - executable used during development process
    * etc . . .
* **Device** - C/C++ implementations of device-specific LoRa packet forwarders.
* **EdgeVisualization** - a tool for visualizing packet flows in IoTEdge.


# Deployment Configurations:
