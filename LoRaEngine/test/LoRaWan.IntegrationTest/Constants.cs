// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    public static class Constants
    {
        // Time to wait between sending message in ms
        public const int DELAY_BETWEEN_MESSAGES = 1000 * 5;

        // Time to wait between sending messages and expecting a serial response
        public const int DELAY_FOR_SERIAL_AFTER_SENDING_PACKET = 150;

        // Time to wait between joining and expecting a serial response
        public const int DELAY_FOR_SERIAL_AFTER_JOIN = 1000;

        public const string TestCollectionName = "ArduinoSerialCollection";

        // Defines Cloud to device message property containing fport value
        internal const string FPORT_MSG_PROPERTY_KEY = "fport";

        // Cloud to device Mac Command property name
        public const string C2D_MSG_PROPERTY_MAC_COMMAND = "CidType";
    }
}