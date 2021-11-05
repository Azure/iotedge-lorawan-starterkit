# 001. Region AS923 implementation

Milestone / Epic: [#412](https://github.com/Azure/iotedge-lorawan-starterkit/issues/412)

Authors: Maggie Salak

## Overview / Problem Statement

The specification of region AS923 defines a `AS923_FREQ_OFFSET` parameter which is used to accommodate different country-specific sub-bands across the 915 - 928 MHz band. The parameter can have one of four different values depending on a country. The corresponding frequency offset in Hz is `AS923_FREQ_OFFSET_HZ = 100 x AS923_FREQ_OFFSET`. The value of the parameter is needed for the purpose of calculating RX2 window frequencies for AS923 region. The parameter is not required for calculating RX1 receive window as it simply uses the same channel as the preceding uplink.

This document summarizes decisions taken for the purpose of implementing support for region AS923.

## In-Scope

- Support for all countries using frequency plan AS923
- Calculation of RX1 downstream frequencies and data rates
- Calculation of RX2 receive window

## Out-of-scope

- MAC commands - support will be added later on as part of [#414](https://github.com/Azure/iotedge-lorawan-starterkit/issues/414)
- Adaptive Data Rate - support will be added later on as part of [#415](https://github.com/Azure/iotedge-lorawan-starterkit/issues/415)

## Decision

The `AS923_FREQ_OFFSET` parameter can be calculated based on the channel 0 and channel 1 frequencies in the LoRa Basics Station configuration. The corresponding channels for region AS923 are defined as follows:

`Channel 0 frequency Hz = 923200000 + AS923_FREQ_OFFSET_HZ`
`Channel 1 frequency Hz = 923400000 + AS923_FREQ_OFFSET_HZ`

Using this formula we will calculate the offset by subtracting 923200000 from the configured channel 0 frequency. We will use the formula for channel 1 frequency to validate the offset value and throw and exception if values are not the same.

In the implementation of region AS923 the frequencies for channel 0 and 1 will be passed to the region-specific constructor where the offset value will be calculated.  
