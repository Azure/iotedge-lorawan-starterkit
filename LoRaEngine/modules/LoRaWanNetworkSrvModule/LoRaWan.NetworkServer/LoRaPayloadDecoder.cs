//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace LoRaWan.NetworkServer
{
    public interface ILoRaPayloadDecoder
    {
        object DecodeAsync(ILoRaDevice loraDeviceInfo, byte[] decryptedPayload);
    }

    public class LoRaPayloadDecoder : ILoRaPayloadDecoder
    {
        public object DecodeAsync(ILoRaDevice loraDeviceInfo, byte[] decryptedPayload)
        {
            return new
            {
                data = LoRaTools.Utils.ConversionHelper.ByteArrayToString(decryptedPayload),
            };
        }
    }
}