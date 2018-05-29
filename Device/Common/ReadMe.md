IoT Edge module that hosts a binary packet forwarder and interprets the module twin configuration data as the global_conf.json for the packet forwarder. 

Docker container for packet forwarder.
Common directory includes several packet forwarders

    - Single Channel Packet Forwarder from https://github.com/hallard/single_chan_pkt_fwd
    Compiled twice for eth0 and wlan0 to work on either Pi0/2/3

    - Semtech based generic packet forwarder

To resolve wiringpi install issues for the Single Channel Gateway board, the wiringpi package is included and consumed by the DockerFile.

Using the VS Code IoT Edge V2 extension, this repository constructs an IoT Edge V2 module that hosts a selected packet forwarder. The global_conf.json can be configured using the IoT Edge V2 Module Twin mechanism in IoT Hub.

The constructed module has a .Net Core entrypoint application that faciliates the Module Twin properties updated event. Writing the received Module Twin global_conf element to the global_conf.json file.

The entrypoint application starts a new process for the chosen packet forwarder. On receipt of new Module Twin changes, this process is recycled - being stopped and restarted.

Change gateway_conf properties using the following Module Twin JSON:

{
  "properties.desired": {
    "configId":3,
      "global_conf":{
        "gateway_conf":{
          "name":"test gateway"
          }
        }
    }
}

NOTE: You must validate property changes are valid for the packet forwarder you specify. Gateway configuration properties vary per packet forwarder implementation.

Issues:
1) Module TWIN JSON support does not supoprt JSON array. So the multiple server definition is broken. Packet Forwarders need modification or the received Module TWIN requires manipulation to construct an JSON array in the output to the global_conf.json file.
2) Only the gateway_conf section of the global_conf.json is available to modify at this time. Other sections of the global_conf.json need to be implemented as required.


When iotedgectl start is complete, the output of the packet forwarder can be viewed in using docker logs <docker process id>


