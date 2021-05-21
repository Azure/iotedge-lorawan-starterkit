// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json.Serialization;

    public class LnsDataFrameJoinRequest
    {
        [JsonPropertyName("msgtype")]
        public string Msgtype { get; set; }

        [JsonPropertyName("MHdr")]
        public uint Mhdr { get; set; }

        [JsonPropertyName("JoinEUI")]
        public string JoinEui { get; set; }

        [JsonPropertyName("DevEUI")]
        public string DevEui { get; set; }

        [JsonPropertyName("DevNonce")]
        public uint DevNonce { get; set; }

        [JsonPropertyName("MIC")]
        public int MIC { get; set; }

        [JsonPropertyName("DR")]
        public int DR { get; set; }

        [JsonPropertyName("Freq")]
        public int Freq { get; set; }

        [JsonPropertyName("upinfo")]
        public UpInfo UpInfo { get; set; }
    }
}
