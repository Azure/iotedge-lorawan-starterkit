//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer.V2
{
    public interface ILoRaPayloadDecoder
    {
        /// <summary>
        /// Decodes a LoRa device message data payload
        /// </summary>
        /// <param name="payload">Data payload received from the device</param>
        /// <param name="fport">FPort used</param>
        /// <param name="sensorDecoder">Decoder configured in the device</param>
        /// <returns></returns>
        Task<JObject> DecodeMessage(byte[] payload, byte fport, string sensorDecoder);
    }
}