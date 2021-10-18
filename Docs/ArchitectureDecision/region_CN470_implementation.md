# Region CN470 implementation

Milestone / Epic: [#416](https://github.com/Azure/iotedge-lorawan-starterkit/issues/416)

Authors: Maggie Salak, Mikhail Chatillon

## Overview / Problem Statement

In order to support CN470 frequency we need to make several decisions.

## In-Scope

* support for OTAA and ABP devices 


## Out-of-scope


## Choices

OTAA join channel is needed to decide which channel plan should be activated for a given device.
We plan to save the join channel in a reported property named `CN470JoinChannel` of the device twin in IoT Hub 
(it will be an int with value in the range 0 - 19). 
The join channel is also needed for computing the RX2 default frequency.

In case of ABP devices the channel plan will need to be provisioned on the IoT Hub by the operator in the desired 
properties of the device twin, since there is no device join in this scenario.
The channel plan will be saved as a join channel number (named `CN470JoinChannel` as above) using the following method 
for determining the correct value:

| Channel plan | `CN470JoinChannel` |
| ------------ | ----|
| 20 MHz Plan A | 0  | 
| 20 MHz Plan B | 8  | 
| 26 MHz Plan A | 10 | 
| 26 MHz Plan B | 15 | 

The region will first check if a join channel is set in the reported properties and, if not, it will retrieve it from desired properties.
