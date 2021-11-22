// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    /// <summary>
    /// Defines a <see cref="LoRaCloudToDeviceMessage"/> that was received by the network server allowing it to abandon, reject and complete.
    /// </summary>
    public class ReceivedLoRaCloudToDeviceMessage : LoRaCloudToDeviceMessage, IReceivedLoRaCloudToDeviceMessage
    {
        public Task<bool> AbandonAsync() => Task.FromResult(true);

        public Task<bool> CompleteAsync() => Task.FromResult(true);

        public Task<bool> RejectAsync() => Task.FromResult(true);

        /// <summary>
        /// Gets the payload bytes.
        /// </summary>
        public byte[] GetPayload()
        {
            if (!string.IsNullOrEmpty(Payload))
            {
                return Encoding.UTF8.GetBytes(Payload);
            }

            if (!string.IsNullOrEmpty(RawPayload))
            {
                try
                {
                    return Convert.FromBase64String(RawPayload);
                }
                catch (FormatException)
                {
                    // Invalid base64 string, return empty payload
                }
            }

            return Array.Empty<byte>();
        }
    }
}
