// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// LoRa payload decoder
    /// </summary>
    public class LoRaPayloadDecoder : ILoRaPayloadDecoder
    {
        private readonly HttpClient httpClient;

        // Http client used by decoders
        // Decoder calls don't need proxy since they will never leave the IoT Edge device
        Lazy<HttpClient> decodersHttpClient;

        public LoRaPayloadDecoder()
        {
            this.decodersHttpClient = new Lazy<HttpClient>(() =>
            {
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
                client.DefaultRequestHeaders.Add("Keep-Alive", "timeout=86400");
                return client;
            });
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayloadDecoder"/> class.
        /// Constructor for unit testing
        /// </summary>
        public LoRaPayloadDecoder(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async ValueTask<DecodePayloadResult> DecodeMessageAsync(string devEUI, byte[] payload, byte fport, string sensorDecoder)
        {
            sensorDecoder = sensorDecoder ?? string.Empty;

            var base64Payload = ((payload?.Length ?? 0) == 0) ? string.Empty : Convert.ToBase64String(payload);

            // Call local decoder (no "http://" in SensorDecoder)
            if (!sensorDecoder.Contains("http://"))
            {
                Type decoderType = typeof(LoRaPayloadDecoder);
                MethodInfo toInvoke = decoderType.GetMethod(sensorDecoder, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);

                if (toInvoke != null)
                {
                    return new DecodePayloadResult(toInvoke.Invoke(null, new object[] { devEUI, payload, fport }));
                }
                else
                {
                    return new DecodePayloadResult()
                    {
                        Error = $"'{sensorDecoder}' decoder not found",
                    };
                }
            }
            else
            {
                // Call SensorDecoderModule hosted in seperate container ("http://" in SensorDecoder)
                // Format: http://containername/api/decodername
                string toCall = sensorDecoder;

                if (sensorDecoder.EndsWith("/"))
                {
                    toCall = sensorDecoder.Substring(0, sensorDecoder.Length - 1);
                }

                // Support decoders that have a parameter in the URL
                // http://decoder/api/sampleDecoder?x=1 -> should become http://decoder/api/sampleDecoder?x=1&devEUI=11&fport=1&payload=12345
                var queryStringParamSeparator = toCall.Contains('?') ? "&" : "?";

                // use HttpUtility to UrlEncode Fport and payload
                var payloadEncoded = HttpUtility.UrlEncode(base64Payload);
                var devEUIEncoded = HttpUtility.UrlEncode(devEUI);

                // Add Fport and Payload to URL
                toCall = $"{toCall}{queryStringParamSeparator}devEUI={devEUIEncoded}&fport={fport.ToString()}&payload={payloadEncoded}";

                // Call SensorDecoderModule
                return await this.CallSensorDecoderModule(devEUI, toCall, payload);
            }
        }

        async Task<DecodePayloadResult> CallSensorDecoderModule(string devEUI, string sensorDecoderModuleUrl, byte[] payload)
        {
            var base64Payload = ((payload?.Length ?? 0) == 0) ? string.Empty : Convert.ToBase64String(payload);

            try
            {
                var httpClientToUse = this.httpClient ?? this.decodersHttpClient.Value;
                HttpResponseMessage response = await httpClientToUse.GetAsync(sensorDecoderModuleUrl);

                if (!response.IsSuccessStatusCode)
                {
                    var badReqResult = await response.Content.ReadAsStringAsync();

                    return new DecodePayloadResult()
                    {
                        Error = $"SensorDecoderModule '{sensorDecoderModuleUrl}' returned bad request.",
                        ErrorDetail = badReqResult ?? string.Empty,
                    };
                }
                else
                {
                    var externalRawResponse = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(externalRawResponse))
                    {
                        try
                        {
                            ReceivedLoRaCloudToDeviceMessage loRaCloudToDeviceMessage = null;
                            var externalDecoderResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(externalRawResponse);
                            if (externalDecoderResponse.TryGetValue(Constants.CLOUD_TO_DEVICE_DECODER_ELEMENT_NAME, out var cloudToDeviceObject))
                            {
                                if (cloudToDeviceObject is JObject jsonObject)
                                {
                                    loRaCloudToDeviceMessage = jsonObject.ToObject<ReceivedLoRaCloudToDeviceMessage>();
                                }

                                externalDecoderResponse.Remove(Constants.CLOUD_TO_DEVICE_DECODER_ELEMENT_NAME);
                            }

                            return new DecodePayloadResult(externalDecoderResponse)
                            {
                                CloudToDeviceMessage = loRaCloudToDeviceMessage
                            };
                        }
                        catch (JsonReaderException)
                        {
                            // not a json object, use as string
                            return new DecodePayloadResult(externalRawResponse);
                        }
                    }
                    else
                    {
                        // not a json object, use as string
                        return new DecodePayloadResult(externalRawResponse);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(devEUI, $"error in decoder handling: {ex.Message}", LogLevel.Error);

                return new DecodePayloadResult()
                {
                    Error = $"Call to SensorDecoderModule '{sensorDecoderModuleUrl}' failed.",
                    ErrorDetail = ex.Message,
                };
            }
        }

        /// <summary>
        /// Value sensor decoding, from <see cref="byte[]"/> to <see cref="DecodePayloadResult"/>
        /// </summary>
        /// <param name="devEUI">Device identifier</param>
        /// <param name="payload">The payload to decode</param>
        /// <param name="fport">The received frame port</param>
        /// <returns>The decoded value as a JSON string</returns>
        public static object DecoderValueSensor(string devEUI, byte[] payload, uint fport)
        {
            var payloadText = ((payload?.Length ?? 0) == 0) ? string.Empty : Encoding.UTF8.GetString(payload);

            if (long.TryParse(payloadText, NumberStyles.Float, CultureInfo.InvariantCulture, out var longValue))
            {
                return new DecodedPayloadValue(longValue);
            }

            if (double.TryParse(payloadText, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
            {
                return new DecodedPayloadValue(doubleValue);
            }

            return new DecodedPayloadValue(payloadText);
        }
    }
}