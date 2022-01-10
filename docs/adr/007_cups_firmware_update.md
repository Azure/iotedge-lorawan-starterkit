# 007. CUPS Protocol Implementation - Firmware Upgrade

**Feature**: [#1189](https://github.com/Azure/iotedge-lorawan-starterkit/issues/1189)  
**Authors**: Daniele Antonio Maggio, Maggie Salak
**Status**: Proposed

## Overview

Firmware upgrades for LoRa Basics™ Station need to be supported in the CUPS protocol implementation, since certain parts of the information exchanged between the Basics Station and the CUPS server are tied to the  a number of fields exchanged between the Station and the CUPS server must correspond to the current version of the Station.

More information on the protocol and can be found [here](https://doc.sm.tc/station/cupsproto.html).

## In-scope

This document focuses on:

- Defining a flow for executing firmware upgrades of the Basics Station
- Defining the changes needed in LoRaWan Network Server for handling firmware upgrades
- Defining the changes needed in device twins stored in IoT Hub for handling Station firmware upgrades
- Defining the changes required in the storage solution used for the CUPS protocol implementaion
- Defining the changes needed in LoRa Device Provisioning CLI for allowing firmware upgrades

## Out-of-scope

