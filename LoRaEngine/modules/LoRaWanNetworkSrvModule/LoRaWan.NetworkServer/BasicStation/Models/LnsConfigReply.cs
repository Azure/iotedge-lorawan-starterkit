// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using LoRaTools.Regions;

    class LnsConfigReply
    {
        private readonly NetworkServerConfiguration configuration;

        public LnsConfigReply(NetworkServerConfiguration configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));
            this.configuration = configuration;
            this.DRs = this.GetDataratesFromRegion();
        }

        [JsonPropertyName("msgtype")]
        public string Msgtype { get; set; } = "router_config";

        public List<int> NetID { get; set; } = new List<int> { 1 };

        [JsonPropertyName("DRs")]
        public List<List<int>> DRs { get; set; }

        [JsonPropertyName("hwspec")]
        public string Hwspec { get; set; } = "sx1301/1";

        [JsonPropertyName("region")]
        public string Region { get; set; } = "EU863";

        [JsonPropertyName("nocca")]
        public bool Nocca { get; set; } = true;

        [JsonPropertyName("nodc")]
        public bool Nodc { get; set; } = true;

        [JsonPropertyName("nodwell")]
        public bool Nodwell { get; set; } = true;

        [JsonPropertyName("sx1301_conf")]
        public List<Sx1301Config> Sx1301_conf { get; set; }

        private List<List<int>> GetDataratesFromRegion()
        {
            var response = new List<List<int>> { };
            foreach (var item in this.configuration.Region.DRtoConfiguration.Values)
            {
                List<int> newSet = new List<int>() { Convert.ToInt32(item.datarate.SpreadingFactor), Convert.ToInt32(item.datarate.BandWidth), 0 };
                response.Add(newSet);
            }

            return response;
        }
    }
}
