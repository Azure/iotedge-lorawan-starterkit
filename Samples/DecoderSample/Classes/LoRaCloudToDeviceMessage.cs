// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SensorDecoderModule.Classes
{
    using Newtonsoft.Json;

    public class LoRaCloudToDeviceMessage
    {
        [JsonProperty("devEUI", NullValueHandling = NullValueHandling.Ignore)]
        public string DevEUI { get; set; }

        [JsonProperty("fport", NullValueHandling = NullValueHandling.Ignore)]
        public byte Fport { get; set; }

        /// <summary>
        /// Payload as base64 string
        /// Use this to send bytes
        /// </summary>
        [JsonProperty("rawPayload", NullValueHandling = NullValueHandling.Ignore)]
        public string RawPayload { get; set; }

        /// <summary>
        /// Payload as string
        /// Use this to send text
        /// </summary>
        [JsonProperty("payload", NullValueHandling = NullValueHandling.Ignore)]
        public string Payload { get; set; }

        [JsonProperty("confirmed", NullValueHandling = NullValueHandling.Ignore)]
        public bool Confirmed { get; set; }

        [JsonProperty("messageId", NullValueHandling = NullValueHandling.Ignore)]
        public string MessageId { get; set; }
    }
}