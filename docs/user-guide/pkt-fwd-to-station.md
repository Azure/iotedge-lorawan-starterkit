# Migrate from Packet Forwarder to LoRa Basics Station

Before upgrading to v2.0.0, please take some time to review this document on how to migrate from Packet Forwarder to LoRa Basics Station.

## 1. Creation of a concentrator device

Azure IoT Edge LoRaWAN Starter Kit v2.0.0 support the ability to de-couple the "concentrator" devices from the LoRaWan Network Server, ideally allowing the same LNS to handle concentrators with different antenna configuration or from completely different regions.

Because of this, any specific configuration of the concentrator is now pushed from a new IoT Hub Device representing the "concentrator" device.

Due to the decoupling, more secure authentication modes are also supported for the connection between the concentrator and the LNS.

The concept of provisioning a IoT Hub Device representing the concentrator is explained in the [concentrator provisioning](station-device-provisioning.md) documentation page.

The supported authentication modes are explained in the [authentication modes](station-authentication-modes.md) documentation page.

## 2. Connection of the concentrator to the LoRaWan Network Server

After the creation of the concentrator device twin in IoT Hub, it will be possible to connect it to the LoRaWan Network Server.

### Pre-built docker module migration

In case you were using the pre-built [Packet Forwarder module](https://github.com/Azure/iotedge-lorawan-starterkit/blob/116e353bd61133acde13dd9ed6f96ca7156544d1/LoRaEngine/modules/LoRaWanPktFwdModule/start_pktfwd.sh), have a look at the following table for migrating the environment variables to the new [Basic Station module configuration][module-configuration] ones:

| Packet Forwarder variable name | Basics Station variable name | Comment |
| -----------------------------  | ---------------------------- | |
| REGION | *N/A* | No region variable is needed in Basics Station as the antenna configuration is pushed from IoT Hub Device Twin via LNS Protocol Implementation |
| NETWORK_SERVER | CUPS_URI and TC_URI | The Network Server address is now mapped to the CUPS_URI and/or TC_URI fields.<br/>More info in [Basic Station module configuration][module-configuration] |
| RESET_PIN | RESET_PIN | Name and functionality are not changing |
| SPI_DEV | SPI_DEV | Name is not changing.<br/>In Basics Station module, it is a number identifying the SPI location where the board should be accessed (i.e.: when X, board accessed at /dev/spidevX.0)<br/>Field defaults to 0 |
| SPI_SPEED | SPI_SPEED | Name and functionality are not changing.<br/>In Basics Station module, default to 8, unique alternative provided is 2 |

Previously, the Packet Forwarder module was built only for SX1301 based devices using SPI communication. Current LoRaBasicsStationModule is built for both SX1301 and SX1302 based devices (starting from v2.1.0).

A more comprehensive list of allowed variables can be found in the [Basic Station module configuration][module-configuration] page.

### Custom built docker module

In case you are not using the pre-built Packet Forwarder module, because of hardware incompatibilities, you can try build your own docker module by starting from the [official Basics Station source code](https://github.com/lorabasics/basicstation).

If you own a USB-FTDI mPCIe RAK833 board, an unofficial and not supported fork of the Basics Station can be found [here](https://github.com/danigian/basicstation)

### Industrial device

In case you are using an industrial device, please make sure that it supports the Basics Station protocols.

The vast majority of recent industrial devices should support the connection to the CUPS/LNS Protocols.

Depending on the desired authentication mode, setting up an industrial device might be as easy as just pointing to the proper websocket endpoint exposed by the LoRaWan Network Server.

If you think there is an issue with our codebase, feel free to open an issue in the [GitHub repository](https://github.com/Azure/iotedge-lorawan-starterkit/issues)

[module-configuration]:
station-module-configuration.md
