// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SensorDecoderModule.Classes
{
    using System.Text;
    using Newtonsoft.Json;

    internal static class LoraDecoders
    {
        private static string DecoderValueSensor(byte[] payload, uint fport)
        {
            // EITHER: Convert a payload containing a string back to string format for further processing
            var result = Encoding.UTF8.GetString(payload);

            // OR: Convert a payload containing binary data to HEX string for further processing
            var result_binary = ConversionHelper.ByteArrayToString(payload);

            // Write code that decodes the payload here.

            // Return a JSON string containing the decoded data
            return JsonConvert.SerializeObject(new { value = result });
        }
    }
}