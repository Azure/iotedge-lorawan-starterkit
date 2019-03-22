// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    public static class Constants
    {
        // Default value of a C2D message id if missing from the message
        internal const string C2D_MSG_ID_PLACEHOLDER = "ConfirmationC2DMessageWithNoId";

        // Name of the upstream message property reporint a confirmed message
        internal const string C2D_MSG_PROPERTY_VALUE_NAME = "C2DMsgConfirmed";

        // Receive window 1 (RX1)
        public const int RECEIVE_WINDOW_2 = 2;

        // Receive window 2 (RX2)
        public const int RECEIVE_WINDOW_1 = 1;

        // Invalid receive window (when trying to resolve the window to use)
        public const int INVALID_RECEIVE_WINDOW = 0;

        /// <summary>
        /// Defines the maximum difference between saved frame counts before we require a change
        /// </summary>
        public const int MAX_FCNT_UNSAVED_DELTA = 10;

        /// <summary>
        /// Amount in which the FcntDown is incremented in single gateway devices to
        /// ensure next value will be correct even if the network died before persisting it
        /// </summary>
        public const int FCNT_DOWN_INCREMENTED_ON_ABP_DEVICE_LOAD = 10;

        /// <summary>
        /// Max allowed framecount Gap
        /// </summary>
        public const uint MAX_FCNT_GAP = 16384;

        // Cloud to device message overhead
        public const int LORA_PROTOCOL_OVERHEAD_SIZE = 8;

        /// <summary>
        /// Property in decoder json response containing the cloud to the device message
        /// </summary>
        public const string CLOUD_TO_DEVICE_DECODER_ELEMENT_NAME = "cloudToDeviceMessage";

        /// <summary>
        /// Convert the time to the packet forward time (millionth of seconds)
        /// </summary>
        public const uint CONVERT_TO_PKT_FWD_TIME = 1000000;

        /// <summary>
        /// Minimum value for device connection keep alive timeout (1 minute)
        /// </summary>
        public const int MIN_KEEP_ALIVE_TIMEOUT = 60;
    }
}