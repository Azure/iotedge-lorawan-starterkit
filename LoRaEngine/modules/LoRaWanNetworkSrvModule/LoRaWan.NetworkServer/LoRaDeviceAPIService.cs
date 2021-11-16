// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// LoRa Device API Service.
    /// </summary>
    public sealed class LoRaDeviceAPIService : LoRaDeviceAPIServiceBase
    {
        private readonly IServiceFacadeHttpClientProvider serviceFacadeHttpClientProvider;

        public LoRaDeviceAPIService(NetworkServerConfiguration configuration,
                                    IServiceFacadeHttpClientProvider serviceFacadeHttpClientProvider)
            : base(configuration)
        {
            this.serviceFacadeHttpClientProvider = serviceFacadeHttpClientProvider;
        }

        public override async Task<uint> NextFCntDownAsync(string devEUI, uint fcntDown, uint fcntUp, string gatewayId)
        {
            StaticLogger.Log(devEUI, $"syncing FCntDown for multigateway", LogLevel.Debug);

            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = GetFullUri($"NextFCntDown?code={AuthCode}&DevEUI={devEUI}&FCntDown={fcntDown}&FCntUp={fcntUp}&GatewayId={gatewayId}");
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                StaticLogger.Log(devEUI, $"error calling the NextFCntDown function, check the function log. {response.ReasonPhrase}", LogLevel.Error);
                return 0;
            }

            var fcntDownString = await response.Content.ReadAsStringAsync();

            if (ushort.TryParse(fcntDownString, out var newFCntDown))
                return newFCntDown;

            return 0;
        }

        public override async Task<DeduplicationResult> CheckDuplicateMsgAsync(string devEUI, uint fcntUp, string gatewayId, uint fcntDown)
        {
            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = GetFullUri($"DuplicateMsgCheck/{devEUI}?code={AuthCode}&FCntUp={fcntUp}&GatewayId={gatewayId}&FCntDown={fcntDown}");

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                StaticLogger.Log(devEUI, $"error calling the DuplicateMsgCheck function, check the function log. {response.ReasonPhrase}", LogLevel.Error);
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync();
            StaticLogger.Log(devEUI, $"deduplication response: '{payload}'", LogLevel.Debug);
            return JsonConvert.DeserializeObject<DeduplicationResult>(payload);
        }

        public override async Task<FunctionBundlerResult> ExecuteFunctionBundlerAsync(string devEUI, FunctionBundlerRequest request)
        {
            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = GetFullUri($"FunctionBundler/{devEUI}?code={AuthCode}");

            var requestBody = JsonConvert.SerializeObject(request);

            using var content = PreparePostContent(requestBody);
            using var response = await client.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                StaticLogger.Log(devEUI, $"error calling the bundling function, check the function log. {response.ReasonPhrase}", LogLevel.Error);
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<FunctionBundlerResult>(payload);
        }

        public override async Task<bool> ABPFcntCacheResetAsync(string devEUI, uint fcntUp, string gatewayId)
        {
            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = GetFullUri($"NextFCntDown?code={AuthCode}&DevEUI={devEUI}&ABPFcntCacheReset=true&GatewayId={gatewayId}&FCntUp={fcntUp}");
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                StaticLogger.Log(devEUI, $"error calling the NextFCntDown function, check the function log, {response.ReasonPhrase}", LogLevel.Error);
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public sealed override Task<SearchDevicesResult> SearchAndLockForJoinAsync(string gatewayID, string devEUI, string devNonce)
            => SearchDevicesAsync(gatewayID: gatewayID, devEUI: devEUI, devNonce: devNonce);

        /// <inheritdoc />
        public sealed override Task<SearchDevicesResult> SearchByDevAddrAsync(string devAddr)
            => SearchDevicesAsync(devAddr: devAddr);

        /// <summary>
        /// Helper method that calls the API GetDevice method.
        /// </summary>
        private async Task<SearchDevicesResult> SearchDevicesAsync(string gatewayID = null, string devAddr = null, string devEUI = null, string appEUI = null, string devNonce = null)
        {
            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();

            var url = BuildUri("GetDevice", new Dictionary<string, string>
            {
                ["code"] = AuthCode,
                ["GateWayId"] = gatewayID,
                ["DevAddr"] = devAddr,
                ["DevEUI"] = devEUI,
                ["AppEUI"] = appEUI,
                ["DevNonce"] = devNonce
            });

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var badReqResult = await response.Content.ReadAsStringAsync();

                    if (string.Equals(badReqResult, "UsedDevNonce", StringComparison.OrdinalIgnoreCase))
                    {
                        return new SearchDevicesResult
                        {
                            IsDevNonceAlreadyUsed = true,
                        };
                    }

                    if (badReqResult != null && badReqResult.StartsWith("JoinRefused", StringComparison.OrdinalIgnoreCase))
                    {
                        return new SearchDevicesResult()
                        {
                            RefusedMessage = badReqResult
                        };
                    }
                }

                StaticLogger.Log(devAddr, $"error calling get device function api: {response.ReasonPhrase}, status: {response.StatusCode}, check the azure function log", LogLevel.Error);

                // TODO: FBE check if we return null or throw exception
                return new SearchDevicesResult();
            }

            var result = await response.Content.ReadAsStringAsync();
            var devices = (List<IoTHubDeviceInfo>)JsonConvert.DeserializeObject(result, typeof(List<IoTHubDeviceInfo>));
            return new SearchDevicesResult(devices);
        }

        /// <inheritdoc />
        public override Task<SearchDevicesResult> SearchByEuiAsync(DevEui eui) =>
            SearchByEuiAsync(eui.ToString("N", null));

        /// <inheritdoc />
        public override Task<SearchDevicesResult> SearchByEuiAsync(StationEui eui) =>
            SearchByEuiAsync(eui.ToString("N", null));

        private async Task<SearchDevicesResult> SearchByEuiAsync(string eui)
        {
            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = BuildUri("GetDeviceByDevEUI", new Dictionary<string, string>
            {
                ["code"] = AuthCode,
                ["DevEUI"] = eui
            });

            var response = await client.GetAsync(new Uri(url.ToString()));
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new SearchDevicesResult();
                }

                StaticLogger.Log(eui, $"error calling get device/station by EUI api: {response.ReasonPhrase}, status: {response.StatusCode}, check the azure function log", LogLevel.Error);

                return new SearchDevicesResult();
            }

            var result = await response.Content.ReadAsStringAsync();
            var devices = (List<IoTHubDeviceInfo>)JsonConvert.DeserializeObject(result, typeof(List<IoTHubDeviceInfo>));
            return new SearchDevicesResult(devices);
        }

        internal Uri GetFullUri(string relativePath)
        {
            // If base URL does not end with a slash, the relative path component is discarded.
            // https://docs.microsoft.com/en-us/dotnet/api/system.uri.-ctor?view=net-5.0#System_Uri__ctor_System_Uri_System_String_
            var baseUrl = URL.OriginalString.EndsWith('/') ? URL : new Uri($"{URL.OriginalString}/");
            return new Uri(baseUrl, relativePath);
        }

        internal Uri BuildUri(string relativePath, IDictionary<string, string> queryParameters)
        {
            var baseUrl = GetFullUri(relativePath);

            var queryParameterSb = new StringBuilder(relativePath);
            queryParameterSb = queryParameters
                .Where(qp => !string.IsNullOrEmpty(qp.Value))
                .Select((qp, i) => $"{(i == 0 ? "?" : "&")}{qp.Key}={qp.Value}")
                .Aggregate(queryParameterSb, (sb, qp) => sb.Append(qp));

            return new Uri(baseUrl, queryParameterSb.ToString());
        }
    }
}
