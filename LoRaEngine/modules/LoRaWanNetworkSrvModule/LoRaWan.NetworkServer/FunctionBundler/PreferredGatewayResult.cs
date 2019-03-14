// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Defines a preferred gateway result
    /// </summary>
    public class PreferredGatewayResult
    {
        public string DevEUI { get; set; }

        public uint RequestFcntUp { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? CurrentFcntUp { get; set; }

        public string PreferredGatewayID { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether there was a conflict in the preferred gateway resolution
        /// </summary>
        /// <remarks>
        /// A conflict happens if a request to resolve the preferred gateway is received with a fcntUp older than the current resolved one.
        /// Causes are the calling gateway took too long to call the function while another device requests have been addressed by other gateways
        /// </remarks>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool Conflict { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ErrorMessage { get; set; }

        public PreferredGatewayResult()
        {
        }

        /// <summary>
        /// Indicates if the preferred gateway resolution was executed successfully
        /// </summary>
        internal bool IsSuccessful() => !this.Conflict && string.IsNullOrEmpty(this.ErrorMessage);
    }
}