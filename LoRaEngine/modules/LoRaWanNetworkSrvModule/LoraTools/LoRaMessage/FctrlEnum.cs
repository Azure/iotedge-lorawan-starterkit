// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    public enum FctrlEnum : short
    {
        FOptLen1 = 0,
        FOptLen2 = 1,
        FOptLen3 = 2,
        FOptLen4 = 4,
        FpendingOrClassB = 16,
        Ack = 32,
        ADRAckReq = 64,
        ADR = 128
    }
}
