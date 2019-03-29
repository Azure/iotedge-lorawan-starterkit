// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    public enum LoRaMessageType : byte
    {
        // Request sent by device to join
        JoinRequest,

        // Response to a join request sent to device
        JoinAccept = 32,

        // Device to cloud message, no confirmation expected
        UnconfirmedDataUp = 64,

        // Cloud to device message, no confirmation expected
        UnconfirmedDataDown = 96,

        // Device to cloud message, confirmation required
        ConfirmedDataUp = 128,

        // Cloud to device message, confirmation required
        ConfirmedDataDown = 160
    }
}
