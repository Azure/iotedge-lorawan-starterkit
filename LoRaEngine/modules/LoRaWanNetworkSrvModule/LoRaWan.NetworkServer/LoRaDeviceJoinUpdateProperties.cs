// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.Regions;

    /// <summary>
    /// Properties of newly joined device.
    /// </summary>
    public class LoRaDeviceJoinUpdateProperties
    {
        public string AppSKey { get; set; }

        public string NwkSKey { get; set; }

        public DevAddr DevAddr { get; set; }

        public string NetID { get; set; }

        public DevNonce DevNonce { get; set; }

        public LoRaRegionType Region { get; set; }

        public string PreferredGatewayID { get; set; }

        public string AppNonce { get; set; }

        /// <summary>
        /// Gets or sets value indicating if region should be saved in reported properties.
        /// </summary>
        public bool SaveRegion { get; set; }

        /// <summary>
        /// Gets or sets value indicating if preferred gateway should be saved in reported properties.
        /// </summary>
        public bool SavePreferredGateway { get; set; }

        /// <summary>
        /// Gets or sets value indicating the join channel plan for region CN470.
        /// </summary>
        public int? CN470JoinChannel { get; set; }

        public StationEui StationEui { get; set; }
    }
}
