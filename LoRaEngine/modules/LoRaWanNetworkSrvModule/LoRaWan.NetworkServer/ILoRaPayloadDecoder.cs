// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    public interface ILoRaPayloadDecoder
    {
        /// <summary>
        /// Decodes a LoRa device message data payload
        /// </summary>
        /// <param name="devEUI">Device identifier</param>
        /// <param name="payload">Data payload received from the device</param>
        /// <param name="fport">FPort used</param>
        /// <param name="sensorDecoder">Decoder configured in the device</param>
        ValueTask<DecodePayloadResult> DecodeMessageAsync(string devEUI, byte[] payload, byte fport, string sensorDecoder);
    }
}