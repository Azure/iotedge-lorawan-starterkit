// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using Newtonsoft.Json;

    /// <summary>
    /// Defines a simple decoded payload value
    /// Represents the value as: { "value":1 }
    /// </summary>
    public class DecodedPayloadValue
    {
        [JsonProperty("value")]
        public object Value { get; set; }

        public DecodedPayloadValue(object value)
        {
            this.Value = value;
        }
    }
}
