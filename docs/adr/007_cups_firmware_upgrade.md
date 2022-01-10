# 007. CUPS Protocol Implementation - Firmware Upgrade

**Feature**:
[#1189](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1189)  

**Authors**: Daniele Antonio Maggio, Maggie Salak

**Status**: Proposed

>This ADR is an extension of [006. CUPS Protocol Implementation - Credential
>management](./006_cups.md) and focuses of firmware upgrades. For details about
>the general CUPS protocol implementation please refer to the other document.

## Overview

Firmware upgrades for LoRa Basics™ Station need to be supported in the CUPS
protocol implementation, since certain pieces of the information exchanged
between the Basics Station and the CUPS server reflect the current version of
the Station.

More information on the protocol and can be found in [The CUPS protocol -
documentation][cupsproto].

## In-scope

This document focuses on:

- Defining a flow for executing firmware upgrades of the Basics Station
- Defining the changes needed in device twins for the concentrator station
  stored in IoT Hub for handling firmware upgrades
- Defining the changes required in the storage solution used for the CUPS
  protocol implementation
- Defining the changes needed in the Azure Function for supporting firmware
  upgrades
- Defining the changes needed in LoRaWan Network Server (LNS) for handling
  firmware upgrades
- Defining the changes needed in LoRa Device Provisioning CLI for allowing
  firmware upgrades

## Out-of-scope

## Decisions

### Context

The CUPS request described in [The CUPS protocol - documentation][cupsproto]
contains the `package` field which indicates the current firmware version of the
Basics Station. The value will need to be updated whenever a firmware upgrade of
the Basics Station is performed by the user.

### Firmware upgrade flow

1. The `package` parameter, along with URL pointing to the location of the
   firmware upgrade binary in a storage account, will be stored in the desired
   properties of the device twin of the concentrator running the Basics Station.
   Those values reflect the up-to-date firmware version that needs to be used
   when communicating with the CUPS server. The firmware upgrade is initiated
   through updating these parameters in the concentrator device twin.

1. The Basics Station sends the CUPS request containing the currently used
   values.

1. The LNS determines if there are discrepanices between the values stored in
   the concentrator twin and the ones provided by the Station. If there are
   differences, the LNS will trigger the firmware upgrade by sending the correct
   values to the Basics Station. The Station will then execute the actual
   firmware upgrade.

### IoT Hub related changes

Only change is related to the concentrator device twin.

In addition to the values already stored in the twin, the following need to be
added:

```json
"cups": {
  // ...
  "package": "1.0.1",
  "fwUrl": "https://...",
  "fwKeyChecksum": 123456,
  "fwSignature": ""
}
```

- **'package'**: current package version of the Station
- **'fwUrl'**: URL pointing to the storage location of the file required to run
  the upgrade
- **'fwKeyChecksum'**: checksum of the key used to sign the digest of the
  firmware upgrade file
- **'fwSignature'**: signature of the uploaded firmware upgrade file

### Storage related changes

We will use the same storage solution as the one selected for the general CUPS
protocol support (storage account). A new container will be used to store the
firmware upgrade files.

### Azure Function related changes

A new endpoint will be added in the Facade Azure Function which will be used to
fetch firmware upgrade files from the storage account.

### Changes in the LoRaWan Network Server

The implementation of `CupsProtocolMessageProcessor` should be extended for
checking the `package` field from the concentrator device twin. In case there
the value is different from the one received from the Basics Station in the CUPS
request, the Network Server will trigger the download of the firmware upgrade
file (using the Facade Function) and populate the CUPS response accordingly, so
that the Station can then execute the upgrade.

### LoRa Device Provisioning CLI changes

A new command will be added to the Device Provisioning CLI which will allow the
user to trigger a firmware upgrade. The command will accept the follwing inputs:

- firmware upgrade file
- signature
- CRC32 checksum of the key used for the signature
- Basics Station package version

The CLI tool will:

1. Upload a blob with the firmware file to the storage account.
1. Update concentrator device twin with the new blob URL, signature and CRC32
   checksum of the key used to generate the signature.

[cupsproto]: https://doc.sm.tc/station/cupsproto.html
