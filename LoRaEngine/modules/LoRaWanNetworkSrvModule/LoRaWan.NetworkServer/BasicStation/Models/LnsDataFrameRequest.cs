// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json.Serialization;

    class LnsDataFrameRequest
    {
        [JsonPropertyName("msgtype")]
        public string Msgtype { get; set; }

        [JsonPropertyName("MHdr")]
        public uint Mhdr { get; set; }

        [JsonPropertyName("DevAddr")]
        public int DevAddr { get; set; }

        [JsonPropertyName("FCtrl")]
        public uint Fctrl { get; set; }

        [JsonPropertyName("FCnt")]
        public uint Fcnt { get; set; }

        [JsonPropertyName("FOpts")]
        public string Fopts { get; set; }

        [JsonPropertyName("FPort")]
        public int Fport { get; set; }

        [JsonPropertyName("FRMPayload")]
        public string FrmPayload { get; set; }

        [JsonPropertyName("MIC")]
        public int Mic { get; set; }
    }
}
