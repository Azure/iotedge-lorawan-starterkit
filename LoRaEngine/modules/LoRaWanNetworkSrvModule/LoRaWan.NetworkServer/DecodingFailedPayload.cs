// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a failed payload decoding
    /// </summary>
    public class DecodingFailedPayload
    {
        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("errorDetail", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorDetail { get; set; }

        public DecodingFailedPayload(string error, string errorDetail)
        {
            this.Error = error;
            this.ErrorDetail = errorDetail;
        }
    }
}
