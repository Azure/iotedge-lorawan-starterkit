// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Text.Json.Serialization;

    internal sealed record JoinAcceptResponse
    {
        [JsonPropertyName("pdu")]
        public string Pdu { get; }

        [JsonPropertyName("DevEui")]
        public string DevEuiString => DevEui.ToString();

        [JsonIgnore]
        public DevEui DevEui { get; }

        [JsonConstructor]
        public JoinAcceptResponse(string pdu, string devEuiString)
        {
            Pdu = pdu;
            DevEui = DevEui.Parse(devEuiString);
        }
    }
}
