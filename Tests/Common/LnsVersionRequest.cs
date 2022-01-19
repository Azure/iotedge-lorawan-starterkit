// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation.Models
{
    using System.Text.Json.Serialization;

    public sealed record LnsVersionRequest
    {
        [JsonPropertyName("msgtype")]
        public string Msgtype { get; } = "version";

        [JsonPropertyName("station")]
        public string Station { get; }

        [JsonPropertyName("firmware")]
        public string Firmware { get; }

        [JsonPropertyName("package")]
        public string Package { get; }

        [JsonPropertyName("model")]
        public string Model { get; }

        [JsonPropertyName("protocol")]
        public int Protocol { get; }

        [JsonPropertyName("features")]
        public string Features { get; }

        public LnsVersionRequest(StationEui station, string firmware, string package, string model, int protocol, string features)
        {
            Station = station.ToString();
            Firmware = firmware;
            Package = package;
            Model = model;
            Protocol = protocol;
            Features = features;
        }
    }
}
