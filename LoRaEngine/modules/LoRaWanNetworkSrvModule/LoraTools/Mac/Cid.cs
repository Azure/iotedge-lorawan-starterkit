// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    public enum Cid
    {
        Zero,
        One,
        LinkCheckCmd,
        LinkADRCmd,
        DutyCycleCmd,
        RXParamCmd,
        DevStatusCmd,
        NewChannelCmd,
        RXTimingCmd,
        TxParamSetupCmd = 0x09
    }
}
