# Region CN470 implementation

Milestone / Epic: [#416](https://github.com/Azure/iotedge-lorawan-starterkit/issues/416)

Authors: Maggie Salak, Mikhail Chatillon

## Overview / Problem Statement

The specification of region CN470 has significant differences compared to regions already supported by the Starterkit. Specifically, there are 4 different frequency channel plans involved and calculation of downstream frequencies requires knowing which channel plan has been activated for a given device during join or device provisioning:

- Plan A for 20 MHz antennas
- Plan B for 20 MHz antennas
- Plan A for 26 MHz antennas
- Plan B for 26 MHz antennas

This document summarizes decisions taken for the purpose of implementing support for region CN470.

## In-Scope

- Support for OTAA and ABP devices
- Correct handling of join requests
- Calculation of downstream frequencies and data rates
- Calculation of RX2 default frequency

## Out-of-scope

- MAC commands - support for MAC commands will be added later on

## Choices

OTAA join channel is needed to determine which channel plan should be activated for a given device and used when calculating downstream frequency.
We plan to save the join channel index in the device twin inside reported properties in IoT Hub. The property would be named `CN470JoinChannel` and the value would be an int in range 0 - 19, corresponding to the 20 possible join channels.

>According to the [specification](https://lora-alliance.org/wp-content/uploads/2021/05/RP002-1.0.3-FINAL-1.pdf), multiple join channels map to the same channel plan, e.g. join channels 0 - 7 are all mapped to Plan A for 20 MHz devices, where each of them has a different frequency. In total there are 20 possible join channels mapped to 4 channel plans as described in Overview.

The join channel is also needed for computing the RX2 default frequency. In this case the join channel index directly determines the correct RX2 default frequency.

In case of ABP devices the channel plan will need to be provisioned on the IoT Hub by the operator in the desired properties of the device twin, since there is no device join in this scenario. For simplicity of the implementation, the channel plan will be stored in the same way as in case of OTAA devices; it would be saved as a join channel index (named `CN470JoinChannel` as in case of OTAA) using the following table for determining the correct value:

| Channel plan | `CN470JoinChannel` |
| ------------ | ----|
| 20 MHz Plan A | 0  |
| 20 MHz Plan B | 8  |
| 26 MHz Plan A | 10 |
| 26 MHz Plan B | 15 |

Since there are no join channels in case of ABP devices, we will define a convention as to which channel index should be used for each channel plan. The suggested way would be to use the lowest index for each of the 4 channel plans, as in the table above.

In the implementation of the region, we will first check if a join channel is set in the reported properties and, if not, we will retrieve it from desired properties. This will also give us the information whether a given device is an OTAA or ABP device.This is needed when calculating the RX2 default frequency. In case of OTAA devices we need to calculated it using the join channel index but in case of ABP it's a constant value.
