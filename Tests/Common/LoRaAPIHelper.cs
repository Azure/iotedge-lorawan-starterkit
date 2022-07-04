// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
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

        public static async Task<bool> ResetADRCache(DevEui devEUI)
        {
            var path = $"FunctionBundler/{devEUI}";
            // the gateway id is only used to identify who is taking the lock when
            // releasing the cache. Hence we do not need a real GW id
            var payload = "{\"AdrRequest\":{\"ClearCache\": true},\"GatewayId\":\"integrationTesting\", \"FunctionItems\": " + (int)FunctionBundlerItemType.ADR + "}";
            return await PostFunctionEndpointAsync(path, payload);
        }

        public static async Task<bool> SendCloudToDeviceMessage(DevEui devEUI, LoRaCloudToDeviceMessage c2dMessage)
        {
            var path = $"cloudtodevicemessage/{devEUI}";
            var json = JsonConvert.SerializeObject(c2dMessage);
            return await PostFunctionEndpointAsync(path, json);
        }

        private static async Task<bool> PostFunctionEndpointAsync(string path, string contentJson, CancellationToken cancellationToken = default)
        {
            var url = new Uri($"{baseUrl}" + path + $"?code={authCode}");
            using var content = PreparePostContent(contentJson);
            using var response = await httpClient.Value.PostAsync(url, content, cancellationToken);
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
