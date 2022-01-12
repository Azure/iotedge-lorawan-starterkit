// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using LoRaWan;
    using Newtonsoft.Json;

    /// <summary>
    /// Result of Send cloud to device message request.
    /// </summary>
    public class SendCloudToDeviceMessageResult
    {
        /// <summary>
        /// Gets or sets the device identifier.
        /// </summary>
        [JsonProperty("devEUI")]
        public string DevEuiString
        {
            get => DevEui.ToHex();
            set => DevEui = DevEui.Parse(value);
        }

        [JsonIgnore]
        public DevEui DevEui { get; set; }

        /// <summary>
        /// Gets or sets the message identifier.
        /// </summary>
        [JsonProperty("messageID")]
        public string MessageID { get; set; }

        /// <summary>
        /// Gets or sets the device class type (A or C at the moment).
        /// </summary>
        [JsonProperty("deviceClassType")]
        public string ClassType { get; set; }
    }
}
