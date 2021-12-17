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
    using LoRaTools.Utils;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// LoRa payload decoder.
    /// </summary>
    public class LoRaPayloadDecoder : ILoRaPayloadDecoder
    {
        private readonly HttpClient httpClient;

        // Http client used by decoders
        // Decoder calls don't need proxy since they will never leave the IoT Edge device
        private readonly Lazy<HttpClient> decodersHttpClient;
        private readonly ILogger<LoRaPayloadDecoder> logger;

        public LoRaPayloadDecoder(ILogger<LoRaPayloadDecoder> logger)
        {
            this.decodersHttpClient = new Lazy<HttpClient>(() =>
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
                client.DefaultRequestHeaders.Add("Keep-Alive", "timeout=86400");
                return client;
            });
            this.logger = logger;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayloadDecoder"/> class.
        /// Constructor for unit testing.
        /// </summary>
        public LoRaPayloadDecoder(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async ValueTask<DecodePayloadResult> DecodeMessageAsync(string devEUI, byte[] payload, byte fport, string sensorDecoder)
        {
            sensorDecoder ??= string.Empty;

            var base64Payload = ((payload?.Length ?? 0) == 0) ? string.Empty : Convert.ToBase64String(payload);

            // Call local decoder (no "http://" in SensorDecoder)
            if (Uri.TryCreate(sensorDecoder, UriKind.Absolute, out var url) && url.Scheme is "http")
            {
                // Support decoders that have a parameter in the URL
                // http://decoder/api/sampleDecoder?x=1 -> should become http://decoder/api/sampleDecoder?x=1&devEUI=11&fport=1&payload=12345

                var query = HttpUtility.ParseQueryString(url.Query);
                query["devEUI"] = devEUI;
                query["fport"] = fport.ToString(CultureInfo.InvariantCulture);
                query["payload"] = base64Payload;

                return await CallSensorDecoderModule(new UriBuilder(url) { Query = query.ToString() }.Uri);
            }
            else
            {
                var decoderType = typeof(LoRaPayloadDecoder);
                var toInvoke = decoderType.GetMethod(sensorDecoder, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);

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
        }

        private async Task<DecodePayloadResult> CallSensorDecoderModule(Uri sensorDecoderModuleUrl)
        {
            try
            {
                var httpClientToUse = this.httpClient ?? this.decodersHttpClient.Value;
                var response = await httpClientToUse.GetAsync(sensorDecoderModuleUrl);

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
                            if (externalDecoderResponse.TryGetValue(Constants.CloudToDeviceDecoderElementName, out var cloudToDeviceObject))
                            {
                                if (cloudToDeviceObject is JObject jsonObject)
                                {
                                    loRaCloudToDeviceMessage = jsonObject.ToObject<ReceivedLoRaCloudToDeviceMessage>();
                                }

                                _ = externalDecoderResponse.Remove(Constants.CloudToDeviceDecoderElementName);
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
            catch (HttpRequestException ex) when (ExceptionFilterUtility.True(() => this.logger.LogError($"error in decoder handling: {ex.Message}")))
            {
                return new DecodePayloadResult()
                {
                    Error = $"Call to SensorDecoderModule '{sensorDecoderModuleUrl}' failed.",
                    ErrorDetail = ex.Message,
                };
            }
        }

        /// <summary>
        /// Value sensor decoding, from <see cref="byte[]"/> to <see cref="DecodePayloadResult"/>.
        /// </summary>
        /// <param name="devEUI">Device identifier.</param>
        /// <param name="payload">The payload to decode.</param>
        /// <param name="fport">The received frame port.</param>
        /// <returns>The decoded value as a JSON string.</returns>
#pragma warning disable CA1801 // Review unused parameters
#pragma warning disable IDE0060 // Remove unused parameter
        // Method is invoked via reflection.
        public static object DecoderValueSensor(string devEUI, byte[] payload, uint fport)
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore CA1801 // Review unused parameters
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

        /// <summary>
        /// Value Hex decoding, from <see cref="byte[]"/> to <see cref="DecodePayloadResult"/>.
        /// </summary>
        /// <param name="devEUI">Device identifier.</param>
        /// <param name="payload">The payload to decode.</param>
        /// <param name="fport">The received frame port.</param>
        /// <returns>The decoded value as a JSON string.</returns>
#pragma warning disable CA1801 // Review unused parameters
#pragma warning disable IDE0060 // Remove unused parameter
        // Method is invoked via reflection and part of a public API.
        public static object DecoderHexSensor(string devEUI, byte[] payload, uint fport)
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore CA1801 // Review unused parameters
        {
            var payloadHex = ((payload?.Length ?? 0) == 0) ? string.Empty : ConversionHelper.ByteArrayToString(payload);
            return new DecodedPayloadValue(payloadHex);
        }
    }
}
