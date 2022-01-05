// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaWan.Core;
    using Newtonsoft.Json;

    public static class LoRaAPIHelper
    {
        private static string authCode;
        private static Uri baseUrl;
        private static ServiceFacadeHttpClientHandler httpHandler;
        private static Lazy<HttpClient> httpClient;

        internal static void Initialize(string functionAppCode, Uri functionAppBaseUrl)
        {
            authCode = functionAppCode;
            baseUrl = functionAppBaseUrl;
            httpHandler = new ServiceFacadeHttpClientHandler(ApiVersion.LatestVersion);
            httpClient = new Lazy<HttpClient>(CreateHttpClient);
        }

        private static HttpClient CreateHttpClient()
        {
            return new HttpClient(httpHandler);
        }

        public static async Task<bool> ResetADRCache(string devEUI)
        {
            var url = new Uri($"{baseUrl}FunctionBundler/{devEUI}?code={authCode}");
            // the gateway id is only used to identify who is taking the lock when
            // releasing the cache. Hence we do not need a real GW id
            var payload = "{\"AdrRequest\":{\"ClearCache\": true},\"GatewayId\":\"integrationTesting\", \"FunctionItems\": " + (int)FunctionBundlerItemType.ADR + "}";

            using var content = PreparePostContent(payload);
            using var response = await httpClient.Value.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }

        public static async Task<bool> SendCloudToDeviceMessage(string devEUI, LoRaCloudToDeviceMessage c2dMessage)
        {
            var url = new Uri($"{baseUrl}cloudtodevicemessage/{devEUI}?code={authCode}");
            var json = JsonConvert.SerializeObject(c2dMessage);
            using var content = PreparePostContent(json);
            using var response = await httpClient.Value.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }

        private static ByteArrayContent PreparePostContent(string requestBody)
        {
            var buffer = Encoding.UTF8.GetBytes(requestBody);
            var byteContent = new ByteArrayContent(buffer);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return byteContent;
        }
    }
}
