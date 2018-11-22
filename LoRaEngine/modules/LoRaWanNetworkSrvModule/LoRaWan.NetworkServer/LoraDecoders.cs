//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace LoRaWan.NetworkServer
{
    class LoraDecoders
    {
        public static async Task<JObject> DecodeMessage(byte[] payload, uint fport, string SensorDecoder)
        {
            string result;
            var base64Payload = Convert.ToBase64String(payload);

            // Call local decoder (no "http://" in SensorDecoder)
            if (!SensorDecoder.Contains("http://"))
            {
                Type decoderType = typeof(LoraDecoders);
                MethodInfo toInvoke = decoderType.GetMethod(
                   SensorDecoder, BindingFlags.Static | BindingFlags.NonPublic);

                if (toInvoke != null)
                {
                    result = (string)toInvoke.Invoke(null, new object[] { payload, fport });
                }
                else
                {
                    result = $"{{\"error\": \"No '{SensorDecoder}' decoder found\", \"rawpayload\": \"{base64Payload}\"}}";
                }
            }
            // Call SensorDecoderModule hosted in seperate container ("http://" in SensorDecoder)
            // Format: http://containername/api/decodername
            else
            {
                string toCall = SensorDecoder;

                if (SensorDecoder.EndsWith("/"))
                {
                    toCall = SensorDecoder.Substring(0, SensorDecoder.Length - 1);
                }

                // use HttpUtility to UrlEncode Fport and payload
                string fportEncoded = HttpUtility.UrlEncode(fport.ToString());
                string payloadEncoded = HttpUtility.UrlEncode(Encoding.ASCII.GetString(payload));

                // Add Fport and Payload to URL
                toCall = $"{toCall}?fport={fportEncoded}&payload={payloadEncoded}";

                // Call SensorDecoderModule
                result = await CallSensorDecoderModule(toCall, payload);
            }

            JObject resultJson;

            // Verify that result is valid JSON.
            try { 
                resultJson = JObject.Parse(result);
            }
            catch
            {
                resultJson = JObject.Parse($"{{\"error\": \"Invalid JSON returned from '{SensorDecoder}'\", \"rawpayload\": \"{base64Payload}\"}}");
            }

            return resultJson;

        }

        private static async Task<string> CallSensorDecoderModule(string sensorDecoderModuleUrl, byte[] payload)
        {
            var base64Payload = Convert.ToBase64String(payload);
            string result = "";

            try
            {
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
                client.DefaultRequestHeaders.Add("Keep-Alive", "timeout=86400");
                HttpResponseMessage response = await client.GetAsync(sensorDecoderModuleUrl);                

                if (!response.IsSuccessStatusCode)
                {
                    var badReqResult = await response.Content.ReadAsStringAsync();

                    result = JsonConvert.SerializeObject(new {
                            error = $"SensorDecoderModule '{sensorDecoderModuleUrl}' returned bad request.",
                            exceptionMessage = badReqResult ?? string.Empty,
                            rawpayload = base64Payload
                        });                     
                }
                else
                {
                    result = await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in decoder handling: {ex.ToString()}", Logger.LoggingLevel.Error);
                
                result = JsonConvert.SerializeObject(new {
                    error = $"Call to SensorDecoderModule '{sensorDecoderModuleUrl}' failed.",
                    exceptionMessage = ex.ToString(),
                    rawpayload = base64Payload
                });
            }

            return result;
        }

        private static string DecoderValueSensor(byte[] payload, uint fport)
        {
            var result = Encoding.ASCII.GetString(payload);            
            return $"{{ \"value\":{result} }}";
        }
    }

}
