// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    public enum Cid
    {
        None,
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
