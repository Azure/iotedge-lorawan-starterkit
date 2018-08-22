//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace LoRaWan.NetworkServer
{


    class LoraDecoders
    {
        public static string DecodeMessage(byte[] payload, uint fport, string SensorDecoder)
        {
            Type decoderType = typeof(LoraDecoders);
            MethodInfo toInvoke = decoderType.GetMethod(
               SensorDecoder, BindingFlags.Static | BindingFlags.NonPublic);

            if (toInvoke != null)
            {
       
                return (string)toInvoke.Invoke(null, new object[] { payload, fport});
            }
            else
            {
                var base64Payload = Convert.ToBase64String(payload);
                return $"{{\"error\": \"No '{SensorDecoder}' decoder found\", \"rawpayload\": \"{base64Payload}\"}}";
            }
        }
               
        private static string DecoderGpsSensor(byte[] payload, uint fport)
        {
            var result = Encoding.ASCII.GetString(payload);
            string[] values = result.Split(':');
            return String.Format("{{\"latitude\": {0} , \"longitude\": {1}}}", values[0], values[1]);
        }
        
        private static string DecoderTemperatureSensor(byte[] payload, uint fport)
        {
            var result = Encoding.ASCII.GetString(payload);
            return String.Format("{{\"temperature\": {0}}}", result);
        }

        private static string DecoderValueSensor(byte[] payload, uint fport)
        {
            var result = Encoding.ASCII.GetString(payload);
            return String.Format("{{\"value\": {0}}}", result);

        }
    }

}
