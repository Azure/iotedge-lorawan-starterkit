// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json.Serialization;

    class LbsClassADownlink
    {
        [JsonPropertyName("msgtype")]
        public LbsMessageType Msgtype { get; set; }

        [JsonPropertyName("DevEui")]
        public string DevEUI { get; set; }

        [JsonPropertyName("dC")]
        public int DC { get; set; } = 0;

        [JsonPropertyName("diid")]
        public long Diid { get; set; }

        [JsonPropertyName("pdu")]
        public string Pdu { get; set; }

        [JsonPropertyName("RxDelay")]
        public ushort RxDelay { get; set; }

        [JsonPropertyName("RX1DR")]
        public ushort RX1DR { get; set; }

        [JsonPropertyName("RX1Freq")]
        public ushort RX1Freq { get; set; }

        [JsonPropertyName("RX2DR")]
        public ushort RX2DR { get; set; }

        [JsonPropertyName("RX2Freq")]
        public ushort RX2Freq { get; set; }

        [JsonPropertyName("priority")]
        public ushort Priority { get; set; }

        [JsonPropertyName("xtime")]
        public long Xtime { get; set; }

        [JsonPropertyName("rctx")]
        public long Rctx { get; set; }
    }
}
