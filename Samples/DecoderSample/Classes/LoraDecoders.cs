// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace SensorDecoderModule.Classes
{
    using System;
    using System.Collections.Generic;
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
        private static readonly Dictionary<uint, string> ttnEvents_ = new Dictionary<uint, string> { {1, "setup"},  {2, "interval"}, {3, "motion"}, {4, "button"}};
        private static string DecoderTheThingsNodeSensor(string devEUI, byte[] payload, uint fport) 
        {

            var eventName = ttnEvents_[fport];
            var battery =  (payload[0] << 8) + payload[1];
            var light = (payload[2] << 8) + payload[3];
            var temperature = (payload[4] & 0x80) > 0 ?  Convert.ToDouble(((0xffff << 16) + (payload[4] << 8) + payload[5])) / 100 : Convert.ToDouble(((payload[4] << 8) + payload[5])) / 100;
 
           var message =  $"{{\"event\": \"{eventName}\",\"battery\": {battery}, \"light\": {light}, \"temperature\": {temperature} }}";
            //Logger.Log(message, Logger.LoggingLevel.Always);
           return message;
        }

         private static string DecoderLansitecTemperatureHumiditySensor(string devEUI, byte[] payload, uint fport) 
        {

            var eventName = ttnEvents_[fport];
            var battery =  (payload[0] << 8) + payload[1];
            var light = (payload[2] << 8) + payload[3];
            var temperature = (payload[4] & 0x80) > 0?  ((0xffff << 16) + (payload[4] << 8) + payload[5]) / 100 : ((payload[4] << 8) + payload[5]) / 100;
 
           var message =  $"{{\"event\": \"{eventName}\",\"battery\": {battery}, \"light\": {light}, \"temperature\": {temperature} }}";
            //Logger.Log(message, Logger.LoggingLevel.Always);
           return message;
        }

        private static string DecoderDecentlabWaterLevelSensor(string devEUI, byte[] payload, uint fport) 
        {
            var bytes = payload;

            var deviceId = (bytes[1] << 8) + bytes[2];
            //var flags = ((bytes[3] << 8) + bytes[4]);

            var integers = new List<int>();

            // convert data to 16-bit integers
            for (var i = 5; i < bytes.Length; i += 2)
            {
                integers.Add((bytes[i] << 8) + bytes[i + 1]);
            }

            var pressure = DecodePressure(integers[0]);
            var temperature = DecodeTemperature(integers[1]);
            var battery = DecodeBattery(integers[2]);

            var message =  $"{{\"deviceId\": \"{deviceId}\",\"battery\": {battery}, \"pressure\": {pressure}, \"temperature\": {temperature} }}";
            //Logger.Log(message, Logger.LoggingLevel.Always);
           return message;
        }

        private static double DecodePressure(double datum)
        {
            return (datum - 16384) / 32768 * (1 - 0) + 0;
        }

        private static double DecodeTemperature(double datum)
        {
            return (datum - 384) / 64000 * 200 - 50;
        }

        private static double DecodeBattery(double datum)
        {
            //return datum / 1000;
            return datum;
        }
  

       private static string DecoderNetvoxTemperatureHumiditySensor(string devEUI, byte[] payload, uint fport) 
        {
             var bytes = payload;
            
            var hex = BitConverter.ToString(bytes);
            var hexValues = hex.Split("-"); 
            var battery = Convert.ToUInt16(hexValues[3], 16) * 100;
            var temperature = Convert.ToInt16($"{hexValues[4]}{hexValues[5]}", 16) * 0.01;
            var humidity = Convert.ToUInt16($"{hexValues[6]}{hexValues[7]}", 16) * 0.01;

            return  $"{{\"battery\": {battery}, \"temperature\": {temperature}, \"humidity\": {humidity} }}";
        }
    }
}