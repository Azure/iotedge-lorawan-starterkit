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
        public const int ReceiveWindow2 = 2;

        // Receive window 2 (RX2)
        public const int ReceiveWindow1 = 1;

        // Invalid receive window (when trying to resolve the window to use)
        public const int InvalidReceiveWindow = 0;

        /// <summary>
        /// Defines the maximum difference between saved frame counts before we require a change.
        /// </summary>
        public const int MaxFcntUnsavedDelta = 10;

        /// <summary>
        /// Amount in which the FcntDown is incremented in single gateway devices to
        /// ensure next value will be correct even if the network died before persisting it.
        /// </summary>
        public const int FcntDownIncrementedOnAbpDeviceLoad = 10;

        /// <summary>
        /// Max allowed framecount Gap.
        /// </summary>
        public const uint MaxFcntGap = 16384;

        // Cloud to device message overhead
        public const int LoraProtocolOverheadSize = 8;

        /// <summary>
        /// Property in decoder json response containing the cloud to the device message.
        /// </summary>
        public const string CloudToDeviceDecoderElementName = "cloudToDeviceMessage";

        /// <summary>
        /// Property in decoder json response containing the cloud to the device message.
        /// </summary>
        public const string CloudToDeviceClearCache = "clearcache";

        /// <summary>
        /// Convert the time to the packet forward time (millionth of seconds).
        /// </summary>
        public const uint ConvertToPktFwdTime = 1000000;

        /// <summary>
        /// Minimum value for device connection keep alive timeout (1 minute).
        /// </summary>
        public const int MinKeepAliveTimeout = 60;

        public const string FacadeServerUrlKey = "FacadeServerUrl";

        public const string FacadeServerAuthCodeKey = "FacadeAuthCode";

        /// <summary>
        /// Log message used to indicate that the same upstream message has already been encountered.
        /// </summary>
        public const string MessageAlreadyEncountered = "because message already encountered";

    }
}
