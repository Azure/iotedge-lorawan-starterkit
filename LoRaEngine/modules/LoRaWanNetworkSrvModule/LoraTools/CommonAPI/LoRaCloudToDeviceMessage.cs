// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.CommonAPI
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// Defines the contract for a LoRa cloud to device message
    /// </summary>
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

        /// <summary>
        /// Gets if the cloud to device message has any payload data (mac commands don't count)
        /// </summary>
        protected bool HasPayload() => !string.IsNullOrEmpty(this.Payload) || !string.IsNullOrEmpty(this.RawPayload);

        /// <summary>
        /// Identifies if the message is a valid LoRa downstream message
        /// </summary>
        /// <param name="errorMessage">Returns the error message in case it fails</param>
        /// <returns>True if the message is valid, false otherwise</returns>
        public virtual bool IsValid(out string errorMessage)
        {
            // ensure fport follows LoRa specification
            // 0    => reserved for mac commands
            // 224+ => reserved for future applications
            if (this.Fport >= LoRaFPort.ReservedForFutureAplications)
            {
                errorMessage = $"invalid fport '{this.Fport}' in cloud to device message '{this.MessageId}'";
                return false;
            }

            // fport 0 is reserved for mac commands
            if (this.Fport == LoRaFPort.MacCommand)
            {
                // Not valid if there is no mac command or there is a payload
                if ((this.MacCommands?.Count ?? 0) == 0 || this.HasPayload())
                {
                    errorMessage = $"invalid MAC command fport usage in cloud to device message '{this.MessageId}'";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }
    }
}
