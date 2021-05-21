// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json.Serialization;

    public class UpInfo
    {
        [JsonPropertyName("rctx")]
        public long Rctx { get; set; }

        [JsonPropertyName("xtime")]
        public long Xtime { get; set; }

        [JsonPropertyName("gpstime")]
        public long Gpstime { get; set; }

        [JsonPropertyName("rssi")]
        public float Rssi { get; set; }

        [JsonPropertyName("snr")]
        public float Snr { get; set; }
    }
}
