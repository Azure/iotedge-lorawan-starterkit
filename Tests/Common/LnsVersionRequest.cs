// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation.Models
{
    using System.Text.Json.Serialization;

    public class LnsVersionRequest
    {
        [JsonPropertyName("msgtype")]
        public string Msgtype { get; set; } = "version";

        [JsonPropertyName("station")]
        public string Station { get; set; }

        [JsonPropertyName("firmware")]
        public string Firmware { get; set; }

        [JsonPropertyName("package")]
        public string Package { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("protocol")]
        public int Protocol { get; set; }

        [JsonPropertyName("features")]
        public string Features { get; set; }

        public LnsVersionRequest(string station, string firmware, string package, string model, int protocol, string features)
        {
            Station = station;
            Firmware = firmware;
            Package = package;
            Model = model;
            Protocol = protocol;
            Features = features;
        }
    }
}
