# Basic Station Certificates Generation

This starter kit is providing a [BasicStation Certificates Generation](https://github.com/Azure/iotedge-lorawan-starterkit/tree/dev/Tools/BasicStation-Certificates-Generation) tool for helping its users to generate LoRaWAN Network Server certificates and Basics Station certificates for **testing** secure communication between a Basics Station client and the CUPS/LNS Protocol Endpoint in Network Server.

The starter kit, and, therefore also this tool, are expecting the same certificate sets for both CUPS and LNS endpoints.

!!! danger "Only for testing"
    In a production environment, you should not use certificates provided by this tool, but generate and sign certificates with a trusted root authority.

## Generate a server certificate

As an example, if you want to generate a server certificate for a LoRaWAN Network Server hosted at `mytest.endpoint.com` and secure the output .PFX with a passphrase, you will need to issue the following script in the `Tools\BasicStation-Certificates-Generation` folder of the StarterKit:

```bash
./certificate-generate.sh server mytest.endpoint.com chosenPfxPassword
```

!!! warning "IMPORTANT"
    when running the Basic Station client with Server Authentication enabled, the common name of the Server Certificate should exactly match the hostname specified in cups.uri/tc.uri. Even though this is decreasing security, it is possible to disable [SNI](https://en.wikipedia.org/wiki/Server_Name_Indication) by setting the '**TLS_SNI**' environment variable to false when executing the Basic Station client (or provided LoRaBasicsStationModule)

The previous command will both generate a self-signed certificate authority (located in the 'ca' folder) and a mytest.endpoint.com.pfx (located in 'server' folder)

You can now follow previous sections instructions on how to import this certificate in the provided LoRaWanNetworkSrvModule.

## Generate a client certificate

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
