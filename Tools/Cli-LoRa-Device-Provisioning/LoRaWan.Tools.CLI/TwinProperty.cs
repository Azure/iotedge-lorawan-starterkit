// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI
{
    public static class TwinProperty
    {
        public const string AppSKey = "AppSKey";
        public const string NwkSKey = "NwkSKey";
        public const string DevAddr = "DevAddr";
        public const string DevNonce = "DevNonce";
        public const string AppEUI = "AppEUI";
        public const string AppKey = "AppKey";
        public const string GatewayID = "GatewayID";
        public const string SensorDecoder = "SensorDecoder";
        public const string FCntUp = "FCntUp";
        public const string FCntDown = "FCntDown";
        public const string DevEUI = "DevEUI";
        public const string NetID = "NetId"; // Must be NetId to be backward compatible
        public const string DownlinkEnabled = "Downlink";
        public const string PreferredWindow = "PreferredWindow"; // (1 or 2)
        public const string Deduplication = "Deduplication"; // None (default), Drop, Mark
        public const string ClassType = "ClassType";
        public const string Supports32BitFCnt = "Supports32BitFCnt";
        public const string FCntResetCounter = "FCntResetCounter";

        // ADR stuff
        public const string DataRate = "DataRate";
        public const string TxPower = "TxPower"; // depend on region 0 - 7 EU or 0 - 14 US
        public const string NbRep = "NbRep"; // 1 - 3
        public const string RX2DataRate = "RX2DataRate";
        public const string RX1DROffset = "RX1DROffset";

        public const string ABPRelaxMode = "ABPRelaxMode";
        public const string FCntUpStart = "FCntUpStart";
        public const string FCntDownStart = "FCntDownStart";
        public const string Region = "Region"; // Defines the region where the device is located
        public const string PreferredGatewayID = "PreferredGatewayID"; // Defines the preferred gateway for a class C device

        public const string RXDelay = "RXDelay";

        // Station twin
        public const string RouterConfig = "routerConfig";
        public const string ClientThumbprint = "clientThumbprint";
        public const string Cups = "cups";
        public const string CupsUri = "cupsUri";
        public const string TcUri = "tcUri";
        public const string CupsCredentialCrc = "cupsCredCrc";
        public const string TcCredentialCrc = "tcCredCrc";
        public const string TcCredentialUrl = "tcCredentialUrl";
        public const string CupsCredentialUrl = "cupsCredentialUrl";
        public const string FirmwareVersion = "package";
        public const string FirmwareUrl = "fwUrl";
        public const string FirmwareKeyChecksum = "fwKeyChecksum";
        public const string FirmwareSignature = "fwSignature";

        /// <summary>
        /// Defines the connection keep alive timeout
        /// </summary>
        public const string KeepAliveTimeout = "KeepAliveTimeout";

        /// <summary>
        /// Defines the tx params in case of a dwell time enabled network
        /// </summary>
        public const string DesiredTxParams = "desiredTxParams";
    }
}
