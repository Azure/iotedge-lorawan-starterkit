// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
#pragma warning disable CA1008 // Enums should have zero value (but values reflect CID from spec which has none)
    public enum Cid
#pragma warning restore CA1008 // Enums should have zero value
    {
        LinkCheckCmd = 2,
        LinkADRCmd = 3,
        DutyCycleCmd = 4,
        RXParamCmd = 5,
        DevStatusCmd = 6,
        NewChannelCmd = 7,
        RXTimingCmd = 8,
        TxParamSetupCmd = 9
    }
}
