// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    /// <summary>
    /// MAC message type (MType) per section 4.2.1 of LoRaWAN Specification 1.0.3.
    /// </summary>

    public enum MacMessageType
    {
        JoinRequest         = 0, // 000
        JoinAccept          = 1, // 001
        UnconfirmedDataUp   = 2, // 010
        UnconfirmedDataDown = 3, // 011
        ConfirmedDataUp     = 4, // 100
        ConfirmedDataDown   = 5, // 101
        RejoinRequest       = 6, // 110 (was reserved/FRU before 1.1)
        Proprietary         = 7, // 111
    }
}
