# LoRa Basics™ Station credentials management in 'Azure IoT Edge LoRaWAN Starter Kit'

Starting with LoRaWAN starter kit 2.0.0, the LoRaWan Network Server runs a WebSocket endpoint compatible with [LNS Protocol](https://doc.sm.tc/station/tcproto.html) from LoRa Basics™ Station.

As it's possible to read in [official LoRa Basics™ Station documentation](https://doc.sm.tc/station/credentials.html) a Basics Station client needs some credentials to establish a secure connection to LNS compatible endpoints.

## Server Authentication

### Importing certificate for server authentication in LoRaWan Network Server module

LoRaWan Network Server IoT Edge module allows to import, from disk, a certificate for server authentication in 'pkcs12' format (.pfx).

Two environment variables need to be set for making this happen:

- **LNS_SERVER_PFX_PATH**: It's the absolute path to the .pfx certificate in the IoT Edge module filesystem (i.e.: '/var/lorastarterkit/certs/lns.pfx')
- **LNS_SERVER_PFX_PASSWORD** *(optional)*: needs to be set if the .pfx was exported with password

As stated above, LNS_SERVER_PFX_PATH is a path inside the IoT Edge module filesystem.

Assuming the .pfx file is located in a folder on the host OS at /mnt/lora/certs, you will need to 'bind' this path to one in the IoT Edge module itself. In order to do so:

- Log into your Azure Portal

- Identify the IoT Edge Device in IoT Hub

- Set the 'LoRaWanNetworkSrvModule' HostConfiguration to include a binding for the folder

  ```json
  "Binds":  [
  	"/mnt/lora/certs/:/var/lorastarterkit/certs/"
  ]
  ```

Additional reference on this process can be found [here](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-access-host-storage-from-module?view=iotedge-2020-11).

### Importing 'tc.trust' in bundled 'LoRaBasicsStationModule'

If you are making use of the bundled 'LoRaBasicsStationModule', it's possible to import a tc.trust certificate in the module itself.

As in previous section, assuming the 'tc.trust' certificate (PEM) is located in a folder on the host OS at /mnt/lora/certs, you can 'bind' this path to one in the IoT Edge module itself. In order to do so:

- Log into your Azure Portal

- Identify the IoT Edge Device in IoT Hub

- Set the 'LoRaBasicsStationModule' HostConfiguration to include a binding for the folder

  ```json
  "Binds":  [
  	"/mnt/lora/certs/:/var/lorastarterkit/certs/"
  ]
  ```

Additional reference on this process can be found [here](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-access-host-storage-from-module?view=iotedge-2020-11).

If needed, it's possible to specify a different target path for tc.trust (other than /var/lorastarterkit/certs/tc.trust).
The expected location of the file can be overridden by using the **'TC_TRUST_PATH'** environment variable (i.e.: setting it to '/var/otherfolder/my.ca' will make the module copy the my.ca file to a tc.trust in the LoRa Basics™ Station working directory)

## Client certification

Currently LoRaWan Network Server does not implement any client certificate validation.

