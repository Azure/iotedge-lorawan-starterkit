// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// LoRa Device API Service.
    /// </summary>
    public sealed class LoRaDeviceAPIService : LoRaDeviceAPIServiceBase
    {
        private readonly IServiceFacadeHttpClientProvider serviceFacadeHttpClientProvider;
        private readonly ILogger<LoRaDeviceAPIService> logger;

        public LoRaDeviceAPIService(NetworkServerConfiguration configuration,
                                    IServiceFacadeHttpClientProvider serviceFacadeHttpClientProvider,
                                    ILogger<LoRaDeviceAPIService> logger)
            : base(configuration)
        {
            this.serviceFacadeHttpClientProvider = serviceFacadeHttpClientProvider;
            this.logger = logger;
        }

        public override async Task<uint> NextFCntDownAsync(string devEUI, uint fcntDown, uint fcntUp, string gatewayId)
        {
            this.logger.LogDebug("syncing FCntDown for multigateway");

            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = GetFullUri($"NextFCntDown?code={AuthCode}&DevEUI={devEUI}&FCntDown={fcntDown}&FCntUp={fcntUp}&GatewayId={gatewayId}");
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                this.logger.LogError($"error calling the NextFCntDown function, check the function log. {response.ReasonPhrase}");
                return 0;
            }

            var fcntDownString = await response.Content.ReadAsStringAsync();

            if (ushort.TryParse(fcntDownString, out var newFCntDown))
                return newFCntDown;

            return 0;
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
                this.logger.LogError($"error calling the bundling function, check the function log. {response.ReasonPhrase}");
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
                this.logger.LogError($"error calling the NextFCntDown function, check the function log, {response.ReasonPhrase}");
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public sealed override Task<SearchDevicesResult> SearchAndLockForJoinAsync(string gatewayID, string devEUI, DevNonce devNonce)
            => SearchDevicesAsync(gatewayID: gatewayID, devEUI: devEUI, devNonce: devNonce);

        /// <inheritdoc />
        public sealed override Task<SearchDevicesResult> SearchByDevAddrAsync(string devAddr)
            => SearchDevicesAsync(devAddr: devAddr);

        /// <summary>
        /// Helper method that calls the API GetDevice method.
        /// </summary>
        private async Task<SearchDevicesResult> SearchDevicesAsync(string gatewayID = null, string devAddr = null, string devEUI = null, string appEUI = null, DevNonce? devNonce = null)
        {
            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();

            var url = BuildUri("GetDevice", new Dictionary<string, string>
            {
                ["code"] = AuthCode,
                ["GateWayId"] = gatewayID,
                ["DevAddr"] = devAddr,
                ["DevEUI"] = devEUI,
                ["AppEUI"] = appEUI,
                ["DevNonce"] = devNonce?.ToString()
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

                this.logger.LogError($"{devAddr} error calling get device function api: {response.ReasonPhrase}, status: {response.StatusCode}, check the azure function log");

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

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new SearchDevicesResult();
                }

                this.logger.LogError($"error calling get device/station by EUI api: {response.ReasonPhrase}, status: {response.StatusCode}, check the azure function log");

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

        public override async Task<string> FetchStationCredentialsAsync(StationEui eui, ConcentratorCredentialType credentialtype, CancellationToken token)
        {
            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = BuildUri("FetchConcentratorCredentials", new Dictionary<string, string>
            {
                ["code"] = AuthCode,
                ["StationEui"] = eui.ToString("N", null),
                ["CredentialType"] = credentialtype.ToString()
            });

            var response = await client.GetAsync(url, token);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is not System.Net.HttpStatusCode.NotFound)
                {
                    this.logger.LogError($"error calling fetch station credentials api: {response.ReasonPhrase}, status: {response.StatusCode}, check the azure function log");
                }

                return string.Empty;
            }

            return await response.Content.ReadAsStringAsync(token);
        }
    }
}
