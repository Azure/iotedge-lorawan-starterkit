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
        public static string DecodeMessage(string payload, string SensorDecoder)
        {
            Type decoderType = typeof(LoraDecoders);
            MethodInfo toInvoke = decoderType.GetMethod(
               SensorDecoder, BindingFlags.Static | BindingFlags.NonPublic);

            if (toInvoke != null)
                return (string)toInvoke.Invoke(null, new object[] { payload });
            else
                return $"{{\"error\": \"No '{SensorDecoder}' decoder found\", \"rawpayload\": \"{payload}\"}}";

        }

        private static string DecoderHealthSensor(string result)
        {
            string[] values = result.Split(':');
            return String.Format("{{\"heartrate\": {0} , \"temperature\": {1}}}", values[0], values[1]);
        }
        private static string DecoderGpsSensor(string result)
        {
            string[] values = result.Split(':');
            return String.Format("{{\"latitude\": {0} , \"longitude\": {1}}}", values[0], values[1]);
        }
        private static string DecoderRotatorySensor(string result)
        {
            return String.Format("{{\"angle\": {0}}}", result);
        }
        private static string DecoderTemperatureSensor(string result)
        {
            return String.Format("{{\"temperature\": {0}}}", result);
        }
    }

}
