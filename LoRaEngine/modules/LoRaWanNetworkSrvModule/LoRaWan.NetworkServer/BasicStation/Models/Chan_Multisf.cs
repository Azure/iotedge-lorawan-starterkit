// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.Models
{
    using System.Text.Json.Serialization;

    public class Chan_Multisf
    {
        [JsonPropertyName("enable")]
        public bool Enable { get; set; }

        [JsonPropertyName("radio")]
        public int Radio { get; set; }

        [JsonPropertyName("if")]
        public int If { get; set; }
    }
}
