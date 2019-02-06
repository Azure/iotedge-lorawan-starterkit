// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    public static class Constants
    {
        // Defines Cloud to device message property containing fport value
        internal const string FPORT_MSG_PROPERTY_KEY = "fport";

        // Fport value reserved for mac commands
        internal const byte LORA_FPORT_RESERVED_MAC_MSG = 0;

        // Starting Fport value reserved for future applications
        internal const byte LORA_FPORT_RESERVED_FUTURE_START = 224;

        // Default value of a C2D message id if missing from the message
        internal const string C2D_MSG_ID_PLACEHOLDER = "ConfirmationC2DMessageWithNoId";

        // Name of the upstream message property reporint a confirmed message
        internal const string C2D_MSG_PROPERTY_VALUE_NAME = "C2DMsgConfirmed";
    }
}