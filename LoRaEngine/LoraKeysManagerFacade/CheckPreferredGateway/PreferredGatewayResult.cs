// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using Newtonsoft.Json;
    using System;

    /// <summary>
    /// Defines a preferred gateway result.
    /// </summary>
    public class PreferredGatewayResult
    {
        public string DevEUI { get; }

        public uint RequestFcntUp { get; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? CurrentFcntUp { get; }

        public string PreferredGatewayID { get; }

        /// <summary>
        /// Gets or sets a value indicating whether there was a conflict in the preferred gateway resolution.
        /// </summary>
        /// <remarks>
        /// A conflict happens if a request to resolve the preferred gateway is received with a fcntUp older than the current resolved one.
        /// Causes are the calling gateway took too long to call the function while another device requests have been addressed by other gateways.
        /// </remarks>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool Conflict { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ErrorMessage { get; set; }

        public PreferredGatewayResult()
        {
        }

        public PreferredGatewayResult(string devEUI, uint fcntUp, LoRaDevicePreferredGateway preferredGateway)
        {
            if (preferredGateway is null) throw new ArgumentNullException(nameof(preferredGateway));

            DevEUI = devEUI;
            RequestFcntUp = fcntUp;
            CurrentFcntUp = preferredGateway.FcntUp;
            PreferredGatewayID = preferredGateway.GatewayID;
            Conflict = fcntUp != preferredGateway.FcntUp;
        }

        public PreferredGatewayResult(string devEUI, uint fcntUp, string errorMessage)
        {
            DevEUI = devEUI;
            RequestFcntUp = fcntUp;
            ErrorMessage = errorMessage;
        }
    }
}
