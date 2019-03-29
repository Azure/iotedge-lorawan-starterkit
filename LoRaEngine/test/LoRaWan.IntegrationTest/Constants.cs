// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    public static class Constants
    {
        /// <summary>
        /// Time to wait between sending message in ms
        /// </summary>
        public const int DELAY_BETWEEN_MESSAGES = 1000 * 5;

        /// <summary>
        /// Time to wait between sending messages and expecting a serial response
        /// </summary>
        public const int DELAY_FOR_SERIAL_AFTER_SENDING_PACKET = 150;

        /// <summary>
        /// Time to wait between joining and expecting a serial response
        /// </summary>
        public const int DELAY_FOR_SERIAL_AFTER_JOIN = 1000;

        public const string TestCollectionName = "ArduinoSerialCollection";

        /// <summary>
        /// Defines Cloud to device message property containing fport value
        /// </summary>
        internal const string FPORT_MSG_PROPERTY_KEY = "fport";

        /// <summary>
        /// Cloud to device Mac Command property name
        /// </summary>
        public const string C2D_MSG_PROPERTY_MAC_COMMAND = "CidType";

        /// <summary>
        /// Convert the time to the packet forward time (millionth of seconds)
        /// </summary>
        public const uint CONVERT_TO_PKT_FWD_TIME = 1000000;
    }
}