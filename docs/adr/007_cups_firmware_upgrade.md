# 007. CUPS Protocol Implementation - Firmware Upgrade

**Feature**:
[#1189](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1189)  

**Authors**: Daniele Antonio Maggio, Maggie Salak

**Status**: Proposed

## Overview

Firmware upgrades for LoRa Basics™ Station need to be supported in the CUPS
protocol implementation, since certain parts of the information exchanged
between the Basics Station and the CUPS server are tied to the  a number of
fields exchanged between the Station and the CUPS server must correspond to the
current version of the Station.

More information on the protocol and can be found [here][cupsproto].

## In-scope

This document focuses on:

- Defining a flow for executing firmware upgrades of the Basics Station
- Defining the changes needed in LoRaWan Network Server for handling firmware
  upgrades
- Defining the changes needed in device twins stored in IoT Hub for handling
  Station firmware upgrades
- Defining the changes required in the storage solution used for the CUPS
  protocol implementaion
- Defining the changes needed in LoRa Device Provisioning CLI for allowing
  firmware upgrades

## Out-of-scope

## Decisions

### Context

The CUPS request described in the [CUPS protocol documentation][cupsproto]
contains a number of fields which are related to the current firmware version of
the Basics Station. Specifically, `station`, `model`, `package` and `keys`
fields are dependent on the Station version and need to be updated whenever a
firmware upgrade of the Basics Station is performed.

### Firmware upgrade flow

- The above mentioned parameters, along with URL pointing to the location of the
  firmware binaries in a storage account, will be stored in the desired
  properties of the device twin of the concentrator running the Basics Station.
  Those values reflect the up-to-date firmware version that needs to be used
  when communicating with the CUPS server. The firmware upgrade is initiated by
  the user through updating of these parameters in the concentrator device twin.

- The Basics Station sends the CUPS request containing the currently used values
  of the same parameters.

- The Network Server will determine if there are discrepanices between the
  values stored in the concentrator twin and the ones provided by the Station.
  If there are differences, the Network Server will trigger the firmware upgrade
  by sending the correct values to the Basics Station. The Station will then
  execute the actual firmware upgrade.

[cupsproto]: https://doc.sm.tc/station/cupsproto.html
