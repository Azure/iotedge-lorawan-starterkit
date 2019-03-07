// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class LoRaCloudToDeviceMessage : ILoRaCloudToDeviceMessage
    {
        [JsonProperty("devEUI", NullValueHandling = NullValueHandling.Ignore)]
        public string DevEUI { get; set; }

        [JsonProperty("fport", NullValueHandling = NullValueHandling.Ignore)]
        public byte Fport { get; set; }

        /// <summary>
        /// Gets or sets payload as base64 string
        /// Use this to send bytes
        /// </summary>
        [JsonProperty("rawPayload", NullValueHandling = NullValueHandling.Ignore)]
        public string RawPayload { get; set; }

        /// <summary>
        /// Gets or sets payload as string
        /// Use this to send text
        /// </summary>
        [JsonProperty("payload", NullValueHandling = NullValueHandling.Ignore)]
        public string Payload { get; set; }

        [JsonProperty("confirmed", NullValueHandling = NullValueHandling.Ignore)]
        public bool Confirmed { get; set; }

        [JsonProperty("messageId", NullValueHandling = NullValueHandling.Ignore)]
        public string MessageId { get; set; }

        [JsonProperty("macCommands", NullValueHandling = NullValueHandling.Ignore)]
        public IList<MacCommand> MacCommands { get; set; }

        public Task<bool> AbandonAsync() => Task.FromResult(true);

        public Task<bool> CompleteAsync() => Task.FromResult(true);

        public Task<bool> RejectAsync() => Task.FromResult(true);

        /// <summary>
        /// Gets the payload bytes
        /// </summary>
        public byte[] GetPayload()
        {
            if (!string.IsNullOrEmpty(this.Payload))
            {
                return Encoding.UTF8.GetBytes(this.Payload);
            }

            if (!string.IsNullOrEmpty(this.RawPayload))
            {
                try
                {
                    return Convert.FromBase64String(this.RawPayload);
                }
                catch (FormatException ex)
                {
                    // Invalid base64 string, return empty payload
                    Logger.Log($"Payload '{this.RawPayload}' is not a valid base64 value: {ex.Message}", LogLevel.Error);
                }
            }

            return new byte[0];
        }

        public bool IsValid(out string errorMessage)
        {
            errorMessage = null;
            return true;
        }
    }
}
