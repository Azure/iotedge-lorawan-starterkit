// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.Models
{
    using System.Text.Json.Serialization;

    class LnsDiscoveryReply
    {
        [JsonPropertyName("router")]
        public string Router { get; set; }

        [JsonPropertyName("muxs")]
        public string Muxs { get; set; }

        [JsonPropertyName("uri")]
        public string Uri { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }
    }
}
