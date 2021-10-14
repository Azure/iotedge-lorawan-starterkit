// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
#pragma warning disable CA1028 // Enum Storage should be Int32
#pragma warning disable CA1027 // Mark enums with FlagsAttribute
    // Not applicable in this case
    public enum LoRaMessageType : byte
#pragma warning restore CA1027 // Mark enums with FlagsAttribute
#pragma warning restore CA1028 // Enum Storage should be Int32
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
