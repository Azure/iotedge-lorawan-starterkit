// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaWan.Shared;

    public static class LoRaAPIHelper
    {
        private static string authCode;
        private static string baseUrl;
        private static ServiceFacadeHttpClientHandler httpHandler;
        private static Lazy<HttpClient> httpClient;

        internal static void Initialize(string functionAppCode, string functionAppBaseUrl)
        {
            authCode = functionAppCode;
            baseUrl = SanitizeApiURL(functionAppBaseUrl);
            httpHandler = new ServiceFacadeHttpClientHandler(ApiVersion.LatestVersion);
            httpClient = new Lazy<HttpClient>(CreateHttpClient);
        }

        private static HttpClient CreateHttpClient()
        {
            return new HttpClient(httpHandler);
        }

        public static async Task<bool> ResetDeviceCache(string devEUI)
        {
            var url = $"{baseUrl}FunctionBundler/{devEUI}?code={authCode}";
            // the gateway id is only used to identify who is taking the lock when
            // releasing the cache. Hence we do not need a real GW id
            var payload = "{\"GatewayId\":\"integrationTesting\", \"FunctionItems\": " + (int)FunctionBundlerItemType.ResetDeviceCache + "}";

            var response = await httpClient.Value.PostAsync(url, PreparePostContent(payload));
            return response.IsSuccessStatusCode;
        }

        private static ByteArrayContent PreparePostContent(string requestBody)
        {
            var buffer = Encoding.UTF8.GetBytes(requestBody);
            var byteContent = new ByteArrayContent(buffer);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return byteContent;
        }

        private static string SanitizeApiURL(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            value = value.Trim();
            if (value.EndsWith('/'))
                return value;

            return string.Concat(value, "/");
        }
    }
}