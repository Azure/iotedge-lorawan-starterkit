// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    class LnsConfigReply
    {
        [JsonPropertyName("msgtype")]
        public string Msgtype { get; set; } = "router_config";

        public List<int> NetID { get; set; } = new List<int> { 1 };

        [JsonPropertyName("DRs")]
        public List<List<int>> DRs { get; set; } = new List<List<int>>
        {
            new List<int> { 12, 125, 0, },
            new List<int> { 11, 125, 0, },
            new List<int> { 10, 125, 0, },
            new List<int> { 9, 125, 0, },
            new List<int> { 8, 125, 0, },
            new List<int> { 7, 125, 0, },
            new List<int> { 7, 250, 0, }
        };

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
    }
}
