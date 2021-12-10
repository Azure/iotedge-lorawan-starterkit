# Basics Station credentials management

Starting with 'Azure IoT Edge LoRaWAN Starter Kit' v2.0.0, the LoRaWan Network Server runs a WebSocket endpoint compatible with [The LNS Protocol](https://doc.sm.tc/station/tcproto.html) and the [CUPS Protocol](https://doc.sm.tc/station/cupsproto.html) from LoRa Basics™ Station.

As described in the official LoRa Basics™ Station documentation - [Credentials](https://doc.sm.tc/station/credentials.html), a Basics™ Station client needs some credentials to establish a secure connection to LNS/CUPS compatible endpoints.

## Server Authentication

### Importing certificate for server authentication in LoRaWan Network Server module

LoRaWan Network Server IoT Edge module allows to import, a certificate in 'pkcs12' format (.pfx) from disk to be used for server authentication for both LNS and CUPS endpoints.

Two environment variables need to be set for making this happen:

- **LNS_SERVER_PFX_PATH**: It's the absolute path to the .pfx certificate in the IoT Edge module filesystem (i.e.: '/var/lorastarterkit/certs/lns.pfx')
- **LNS_SERVER_PFX_PASSWORD** *(optional)*: needs to be set if the .pfx was exported with password

Assuming the .pfx file is located in a folder on the host OS at /mnt/lora/certs, you will need to 'bind' this path to one in the IoT Edge module itself. In order to do so:

1. Log into your Azure Portal

2. Identify the IoT Edge Device in IoT Hub

3. Set the 'LoRaWanNetworkSrvModule' HostConfiguration to include a binding for the folder

    ```json
    "Binds":  [
    "/mnt/lora/certs/:/var/lorastarterkit/certs/"
    ]
    ```

Additional information on this process can be found in the documentation - [Use IoT Edge device local storage from a module](https://docs.microsoft.com/azure/iot-edge/how-to-access-host-storage-from-module?view=iotedge-2020-11).

### Importing 'tc.trust/cups.trust' in bundled 'LoRaBasicsStationModule'

If you are making use of the bundled 'LoRaBasicsStationModule', it's possible to import a tc.trust/cups.trust certificate in the module itself.

The default path where the trust file(s) will be searched is '/var/lorastarterkit/certs/'. As described in the previous section, it is possible to bind a folder on the host os to the one mentioned above.

The default path of the tc.trust file can be overridden by using the **'TC_TRUST_PATH'** environment variable (i.e.: setting it to '/var/otherfolder/my.ca' will make the module copy the my.ca file to a tc.trust in the LoRa Basics™ Station working directory).

Same can be done for a cups.trust file by overriding the **'CUPS_TRUST_PATH'** environment variable

## Client certification

The LoRaWan Network Server implementation provided by this starter kit is allowing client authentication from a Basics Station client.

### Importing 'tc.crt/tc.key' in bundled 'LoRaBasicsStationModule'

If you are making use of the bundled 'LoRaBasicsStationModule', it's possible to import a client certificate (.crt + .key files) in the module itself.

The default path where the files will be searched is '/var/lorastarterkit/certs/'. As described in the previous section, it is possible to bind a folder on the host os to the one mentioned above.

The default path of the tc.crt/tc.key/cups.crt/cups.key file can be overridden by using the related environment variable (i.e.: setting any of **'TC_CRT_PATH'**, **'TC_KEY_PATH'**, **'CUPS_CRT_PATH'**, **'CUPS_KEY_PATH'** to '/var/otherfolder/my.crt' will make the module copy the my.crt file to a *.crt in the LoRa Basics™ Station working directory).

### Providing a list of allowed client thumbprints for connection

Server side, the validation of the certificate is happening by comparing the thumbprint of the certificate provided for authentication with a list of allowed thumbprints to be stored in the Concentrator Twin (more information on 'clientThumbprint' property of Twin in [related ADR](https://azure.github.io/iotedge-lorawan-starterkit/dev/adr/006_cups/))

When using the provided **Cli-LoRa-Device-Provisioning** tool to provision a concentrator device to IoT Hub, you can pass the **'--client-certificate-thumbprint'** option to specify the thumbprint of an allowed certificate.

If you can't use the tool, when creating the Concentrator device in IoT Hub, all you need to do is to add a desired property named '**clientThumbprint**' (being an array of allowed certificate thumbprints) and specify the thumbprint in this array.
