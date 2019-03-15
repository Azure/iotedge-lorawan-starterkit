// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.CommonAPI;
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

        [JsonProperty("errorDetail", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorDetail { get; set; }

        /// <summary>
        /// Gets or sets a message to be sent to the device (optional)
        /// Assigning a value to <see cref="ILoRaCloudToDeviceMessage.DevEUI"/> will send the message to a class C device
        /// </summary>
        [JsonProperty(Constants.CLOUD_TO_DEVICE_DECODER_ELEMENT_NAME, NullValueHandling = NullValueHandling.Ignore)]
        public ReceivedLoRaCloudToDeviceMessage CloudToDeviceMessage { get; set; }

        public DecodePayloadResult(object value)
        {
            this.Value = value;
        }

        public DecodePayloadResult()
        {
        }

        public object GetDecodedPayload()
        {
            if (!string.IsNullOrEmpty(this.Error) ||
                !string.IsNullOrEmpty(this.ErrorDetail))
            {
                return new DecodingFailedPayload(this.Error, this.ErrorDetail);
            }

            return this.Value;
        }
    }
}
