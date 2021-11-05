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




