// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Text.Json.Serialization;

    internal sealed record JoinAcceptResponse
    {
        [JsonPropertyName("pdu")]
        public string Pdu { get; set; }

        [JsonPropertyName("DevEui")]
        public string DevEuiString
        {
            get => DevEui.ToString();
            set => DevEui = DevEui.Parse(value);
        }

        [JsonIgnore]
        public DevEui DevEui { get; set; }
    }
}
