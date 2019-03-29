// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Defines a <see cref="LoRaCloudToDeviceMessage"/> that was received by the network server allowing it to abandon, reject and complete
    /// </summary>
    public class ReceivedLoRaCloudToDeviceMessage : LoRaCloudToDeviceMessage, IReceivedLoRaCloudToDeviceMessage
    {
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
    }
}
