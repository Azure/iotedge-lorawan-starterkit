// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Threading.Tasks;
    using LoRaWan;

    public interface ILoRaPayloadDecoder
    {
        /// <summary>
        /// Decodes a LoRa device message data payload.
        /// </summary>
        /// <param name="devEui">Device identifier.</param>
        /// <param name="payload">Data payload received from the device.</param>
        /// <param name="fport">FPort used.</param>
        /// <param name="sensorDecoder">Decoder configured in the device.</param>
        ValueTask<DecodePayloadResult> DecodeMessageAsync(DevEui devEui, byte[] payload, FramePort fport, string sensorDecoder);
    }
}
