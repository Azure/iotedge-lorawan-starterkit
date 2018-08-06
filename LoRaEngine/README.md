# LoRaEngine

A **.NET Standard 2.0** solution with the following projects:

* **PacketForwarderHost** - executable and IoTEdge configuration files.
* **UDPListener** - executable
* **LoRaTools** - library
* **LoRaServer** - IoT edge module executable, Dockerfile, etc.
* **DevTools** - submodule folder, check it out using the command 
              ` git submodule update --init --recursive ` and add the projects to the visual studio solution
  * **PacketForwarderSimulator** - executable
  * **DevTool1** - executable used during development process
  * **DevTool2** - executable used during development process
  * etc . . .

**NOTE:** Until we have a unit test framework in place, we are relying on small executables, maintained by each developer, for the purposes of testing and demonstrating module functionality. Each of these executables should have a README.md file that demonstrates how to run the code.
