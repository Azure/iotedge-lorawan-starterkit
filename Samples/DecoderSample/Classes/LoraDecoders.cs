// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SensorDecoderModule.Classes
{
    using System;
    using System.Text;
    using Newtonsoft.Json;

    internal static class LoraDecoders
    {
        private static string DecoderValueSensor(string devEUI, byte[] payload, byte fport)
        {
            // EITHER: Convert a payload containing a string back to string format for further processing
            var result = Encoding.UTF8.GetString(payload);

            // OR: Convert a payload containing binary data to HEX string for further processing
            var result_binary = ConversionHelper.ByteArrayToString(payload);

            // Write code that decodes the payload here.

            // Return a JSON string containing the decoded data
            return JsonConvert.SerializeObject(new { value = result });

        }

        private static string RelayToClassC(string devEUI, byte[] payload, byte fport)
        {
            // EITHER: Convert a payload containing a string back to string format for further processing
            var decodedValue = Encoding.UTF8.GetString(payload);

            // Write code that decodes the payload here.

            // Return a JSON string containing the decoded data
            var resultObject = new 
            {
                value = decodedValue,
                cloudToDeviceMessage = new LoRaCloudToDeviceMessage()
                {
                    DevEUI = "12300000000CCCCC",
                    Payload = "Hello",
                    // RawPayload = "AQIC", // -> Sends 0x01 0x02 0x03 (in base64)
                    Confirmed = false,
                    Fport = fport,
                    MessageId = Guid.NewGuid().ToString(),
                }
            };

            // Return a JSON string containing the decoded data
            return JsonConvert.SerializeObject(resultObject);

        }
    }
}