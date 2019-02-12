// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SensorDecoderModule.Classes
{
    using Newtonsoft.Json;

    /// <summary>
    /// Defines the result of a decoder
    /// </summary>
    public class DecodePayloadResult
    {
        /// <summary>
        /// Gets or sets the decoded value that will be sent to IoT Hub
        /// </summary>
        [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
        public object Value { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string Error { get; set; }

        [JsonProperty("rawPayload", NullValueHandling = NullValueHandling.Ignore)]
        public string RawPayload { get; set; }

        /// <summary>
        /// Gets or sets a message to be sent to the device (optional)
        /// Assigning a value to <see cref="LoRaCloudToDeviceMessage.DevEUI"/> will send the message to a class C device
        /// </summary>
        [JsonProperty("cloudToDeviceMessage", NullValueHandling = NullValueHandling.Ignore)]
        public LoRaCloudToDeviceMessage CloudToDeviceMessage { get; set; }

        public DecodePayloadResult(object value)
        {
            this.Value = value;
        }
    }
}