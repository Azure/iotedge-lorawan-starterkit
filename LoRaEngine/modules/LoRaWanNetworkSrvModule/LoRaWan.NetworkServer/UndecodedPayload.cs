// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Defines a payload that was not decoded (one was not configure for the device)
    /// </summary>
    public class UndecodedPayload
    {
        private readonly string undecodedValue;

        [JsonProperty("value")]
        public object Value => this.undecodedValue;

        public UndecodedPayload(byte[] payloadData)
        {
            this.undecodedValue = (payloadData == null) ? string.Empty : Convert.ToBase64String(payloadData);
        }
    }
}
