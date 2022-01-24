# Authentication modes

Starting with 'Azure IoT Edge LoRaWAN Starter Kit' v2.0.0, the LoRaWan Network Server runs a WebSocket endpoint compatible with [The LNS Protocol](https://doc.sm.tc/station/tcproto.html) and the [CUPS Protocol](https://doc.sm.tc/station/cupsproto.html) from LoRa Basics™ Station.

As described in the official LoRa Basics™ Station documentation - [Credentials](https://doc.sm.tc/station/credentials.html), a Basics™ Station client needs some credentials to establish a secure connection to LNS/CUPS compatible endpoints.

## Supported authentication modes

The following table describes the supported authentication modes with the LoRaWan Network Server provided in Azure IoT Edge LoRaWAN Starter Kit

|                                         | LNS Endpoint | CUPS Endpoint |
| --------------------------------------- | :----------: | :-----------: |
| No Authentication                       |      ✔️       |       ❌       |
| Server Authentication only              |      ✔️       |       ❌       |
| Mutual (server + client) authentication |      ✔️       |       ✔️       |

## Server Authentication

### Importing certificate for server authentication in LoRaWan Network Server module

LoRaWan Network Server IoT Edge module allows to import a certificate in 'pkcs12' format (.pfx) to be used for server authentication for both LNS and CUPS endpoints.

Two environment variables need to be set in the deployment manifest for making this happen:

- **LNS_SERVER_PFX_PATH**: It's the absolute path to the .pfx certificate in the IoT Edge module filesystem (i.e.: `/var/lorastarterkit/certs/lns.pfx`)
- **LNS_SERVER_PFX_PASSWORD** *(optional)*: needs to be set if the .pfx was exported with password

Instructions on how to modify deployment manifest can be found in documentation ([Learn how to deploy modules and establish routes in IoT Edge](https://docs.microsoft.com/en-us/azure/iot-edge/module-composition))

Assuming the .pfx file is located in a folder on the host OS at `/mnt/lora/certs`, you will need to 'bind' this path to one in the IoT Edge module itself. In order to do so:

1. Log into your Azure Portal

2. Identify the IoT Edge Device in IoT Hub

3. Set the 'LoRaWanNetworkSrvModule' HostConfiguration to include a binding for the folder under "Container Create Options"

    ```json
    "Binds":  [
    "/mnt/lora/certs/:/var/lorastarterkit/certs/"
    ]
    ```

Additional information on this process can be found in the documentation - [Use IoT Edge device local storage from a module](https://docs.microsoft.com/azure/iot-edge/how-to-access-host-storage-from-module?view=iotedge-2020-11).

### Importing 'tc.trust/cups.trust' in bundled 'LoRaBasicsStationModule'

If you are making use of the bundled 'LoRaBasicsStationModule', it's possible to import a tc.trust/cups.trust certificate in the module itself.

The default path where the trust file(s) will be searched is `/var/lorastarterkit/certs/`. As described in the previous section, it is possible to bind a folder on the host OS to the one mentioned above.

The default path of the tc.trust file can be overridden by using the **'TC_TRUST_PATH'** environment variable (i.e.: setting it to `/var/otherfolder/my.ca` will make the module copy the my.ca file to a tc.trust in the LoRa Basics™ Station working directory).

Same can be done for a cups.trust file by overriding the **'CUPS_TRUST_PATH'** environment variable

## Client authentication

The LoRaWan Network Server implementation provided by this starter kit is allowing client authentication from a Basics Station client.

### Changing 'Client Certificate Mode' in 'LoRaWan Network Server module' and trusting certificate chain

By default, the 'LoRaWanNetworkSrvModule' is not accepting/requiring any client certificate for its exposed endpoints.

It is possible to modify the behavior of the module to actually either allow or require a client certificate for accessing to its endpoints.

For example, to always require a client certificate for reaching LNS/CUPS endpoints, you should set the **'CLIENT_CERTIFICATE_MODE'** environment variable in 'LoRaWanNetworkSrvModule' to a value of '2' or 'RequireCertificate'.

Supported values for 'CLIENT_CERTIFICATE_MODE' environment variables are:

- `0` or `NoCertificate`: client certificate is not required and will not be requested from clients
- `1` or `AllowCertificate`: client certificate will be requested; however, authentication will not fail if a certificate is not provided by the client
- `2` or `RequireCertificate`: client certificate will be requested, and the client must provide a valid certificate for authentication to succeed

When a client certificate is used, two verifications are done on the 'LoRaWanNetworkSrvModule' side:

1. The certificate chain is validated.  
This implies that `LoRaWanNetworkSrvModule` will need to trust the root/intermediate certificates that generated the client one.  
If your certificate was not signed by a well-known CA, you will need to import one (or more) `.crt` PEM-encoded files for trusting the chain. The instructions for mounting volumes can be found in previous paragraphs.  
The client certificate will be searched by default at `/var/lorastarterkit/certs/client.ca.crt`.  
You can override this path to pass multiple files by specifying a `;` separated list of paths in the `CLIENT_CA_PATH` environment variable.  
Exempli gratia, if you need to trust one root certificate located at module path `/var/lorastarterkit/certs/root.crt` plus one intermediate certificate at `/var/lorastarterkit/certs/intermediate.crt` you will need to set the CLIENT_CA_PATH environment variable to `/var/lorastarterkit/certs/root.crt;/var/lorastarterkit/certs/intermediate.crt`
1. The certificate thumbprint gets compared with what is stored in the Concentrator Twin in IoT Hub.

### Importing 'tc.crt/tc.key/cups.crt/cups.key' in bundled 'LoRaBasicsStationModule'

If you are making use of the bundled 'LoRaBasicsStationModule', it's possible to import a client certificate (.crt + .key files) in the module itself.

The default path where the files will be searched is `var/lorastarterkit/certs/`. As described in the previous section, it is possible to bind a folder on the host OS to the one mentioned above.

The default path of the tc.crt/tc.key/cups.crt/cups.key file can be overridden by using the related environment variable (i.e.: setting any of **'TC_CRT_PATH'**, **'TC_KEY_PATH'**, **'CUPS_CRT_PATH'**, **'CUPS_KEY_PATH'** to `/var/otherfolder/my.crt` will make the module copy the my.crt file to a *.crt in the LoRa Basics™ Station working directory).

### Providing a list of allowed client thumbprints for connection

At the server side, the validation of the certificate is happening by comparing the thumbprint of the certificate provided for authentication with a list of allowed thumbprints stored in the Concentrator Twin (more information on 'clientThumbprint' property of Twin in [related ADR](https://azure.github.io/iotedge-lorawan-starterkit/dev/adr/006_cups/))

When using the provided **Cli-LoRa-Device-Provisioning** tool to provision a concentrator device to IoT Hub, you can pass the **'--client-certificate-thumbprint'** option to specify the thumbprint of an allowed certificate.

If you can't use the tool, when creating the Concentrator device in IoT Hub, all you need to do is to add a desired property named '**clientThumbprint**' (being an array of allowed certificate thumbprints) and specify the thumbprint in this array.

## Self-signed certificates generator (for tests only)

This starter kit is providing a [BasicStation Certificates Generation](https://github.com/Azure/iotedge-lorawan-starterkit/tree/dev/Tools/BasicStation-Certificates-Generation) tool for helping its users to generate LoRaWAN Network Server certificates and Basics Station certificates for **testing** secure communication between a Basics Station client and the CUPS/LNS Protocol Endpoint in Network Server.

The starter kit, and, therefore also this tool, are expecting the same certificate sets for both CUPS and LNS endpoints.

!!! danger "Only for testing"
    In a production environment, you should not use certificates provided by this tool, but generate and sign certificates with a trusted root authority.

### Generate a server certificate

As an example, if you want to generate a server certificate for a LoRaWAN Network Server hosted at `mytest.endpoint.com` and secure the output .PFX with a passphrase, you will need to issue the following script in the `Tools\BasicStation-Certificates-Generation` folder of the StarterKit:

```bash
./certificate-generate.sh server mytest.endpoint.com chosenPfxPassword
```

!!! warning "IMPORTANT"
    when running the Basic Station client with Server Authentication enabled, the common name of the Server Certificate should exactly match the hostname specified in cups.uri/tc.uri. Even though this is decreasing security, it is possible to disable [SNI](https://en.wikipedia.org/wiki/Server_Name_Indication) by setting the '**TLS_SNI**' environment variable to false when executing the Basic Station client (or provided LoRaBasicsStationModule)

The previous command will both generate a self-signed certificate authority (located in the 'ca' folder) and a mytest.endpoint.com.pfx (located in 'server' folder)

You can now follow previous sections instructions on how to import this certificate in the provided LoRaWanNetworkSrvModule.

### Generate a client certificate

As an example, if you want to generate a client certificate for a Basic Station with DevEUI 'AABBCCFFFE001122', you will need to issue the following command

```bash
./certificate-generate.sh client AABBCCFFFE001122
```

!!! warning "IMPORTANT"
    for client authentication to successfully work, the Common Name of the certificate should exactly match the DevEUI of the Basic Station.

The previous command will generate the following files in the 'client' subfolder:

- AABBCCFFFE001122.crt (the client certificate in DER format)
- AABBCCFFFE001122.key (the client certificate key in DER format)
- AABBCCFFFE001122.trust (the root certificate in DER format)
- AABBCCFFFE001122.bundle (the concatenation of the three above mentioned files, useful if you want to use CUPS)

As soon as certificates are generated, you can now follow previous sections instructions on how to import this certificate in the provided LoRaBasicsStationModule or you can just copy the needed files to your Basic Station compatible concentrator.

--8<-- "includes/abbreviations.md"