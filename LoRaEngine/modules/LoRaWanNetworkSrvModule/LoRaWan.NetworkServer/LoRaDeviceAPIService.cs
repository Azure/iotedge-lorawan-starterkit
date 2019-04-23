// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer.ADR;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// LoRa Device API Service
    /// </summary>
    public sealed class LoRaDeviceAPIService : LoRaDeviceAPIServiceBase
    {
        private readonly IServiceFacadeHttpClientProvider serviceFacadeHttpClientProvider;

        public LoRaDeviceAPIService(NetworkServerConfiguration configuration, IServiceFacadeHttpClientProvider serviceFacadeHttpClientProvider)
            : base(configuration)
        {
            this.serviceFacadeHttpClientProvider = serviceFacadeHttpClientProvider;
        }

        public override async Task<uint> NextFCntDownAsync(string devEUI, uint fcntDown, uint fcntUp, string gatewayId)
        {
            Logger.Log(devEUI, $"syncing FCntDown for multigateway", LogLevel.Debug);

            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = $"{this.URL}NextFCntDown?code={this.AuthCode}&DevEUI={devEUI}&FCntDown={fcntDown}&FCntUp={fcntUp}&GatewayId={gatewayId}";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log(devEUI, $"error calling the NextFCntDown function, check the function log. {response.ReasonPhrase}", LogLevel.Error);
                return 0;
            }

            string fcntDownString = await response.Content.ReadAsStringAsync();

            if (ushort.TryParse(fcntDownString, out var newFCntDown))
                return newFCntDown;

            return 0;
        }

        public override async Task<DeduplicationResult> CheckDuplicateMsgAsync(string devEUI, uint fcntUp, string gatewayId, uint fcntDown)
        {
            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = $"{this.URL}DuplicateMsgCheck/{devEUI}?code={this.AuthCode}&FCntUp={fcntUp}&GatewayId={gatewayId}&FCntDown={fcntDown}";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log(devEUI, $"error calling the DuplicateMsgCheck function, check the function log. {response.ReasonPhrase}", LogLevel.Error);
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync();
            Logger.Log(devEUI, $"deduplication response: '{payload}'", LogLevel.Debug);
            return JsonConvert.DeserializeObject<DeduplicationResult>(payload);
        }

        public override async Task<FunctionBundlerResult> ExecuteFunctionBundlerAsync(string devEUI, FunctionBundlerRequest request)
        {
            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = $"{this.URL}FunctionBundler/{devEUI}?code={this.AuthCode}";

            var requestBody = JsonConvert.SerializeObject(request);

            var response = await client.PostAsync(url, PreparePostContent(requestBody));
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log(devEUI, $"error calling the bundling function, check the function log. {response.ReasonPhrase}", LogLevel.Error);
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<FunctionBundlerResult>(payload);
        }

        public override async Task<bool> ABPFcntCacheResetAsync(string devEUI, uint fcntUp, string gatewayId)
        {
            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = $"{this.URL}NextFCntDown?code={this.AuthCode}&DevEUI={devEUI}&ABPFcntCacheReset=true&GatewayId={gatewayId}&FCntUp={fcntUp}";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log(devEUI, $"error calling the NextFCntDown function, check the function log, {response.ReasonPhrase}", LogLevel.Error);
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public sealed override Task<SearchDevicesResult> SearchAndLockForJoinAsync(string gatewayID, string devEUI, string appEUI, string devNonce)
            => this.SearchDevicesAsync(gatewayID: gatewayID, devEUI: devEUI, appEUI: appEUI, devNonce: devNonce);

        /// <inheritdoc />
        public sealed override Task<SearchDevicesResult> SearchByDevAddrAsync(string devAddr)
            => this.SearchDevicesAsync(devAddr: devAddr);

        /// <summary>
        /// Helper method that calls the API GetDevice method
        /// </summary>
        async Task<SearchDevicesResult> SearchDevicesAsync(string gatewayID = null, string devAddr = null, string devEUI = null, string appEUI = null, string devNonce = null)
        {
            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = new StringBuilder();
            url.Append(this.URL)
                .Append("GetDevice?code=")
                .Append(this.AuthCode);

            if (!string.IsNullOrEmpty(gatewayID))
            {
                url.Append("&GatewayId=")
                    .Append(gatewayID);
            }

            if (!string.IsNullOrEmpty(devAddr))
            {
                url.Append("&DevAddr=")
                    .Append(devAddr);
            }

            if (!string.IsNullOrEmpty(devEUI))
            {
                url.Append("&DevEUI=")
                    .Append(devEUI);
            }

            if (!string.IsNullOrEmpty(appEUI))
            {
                url.Append("&AppEUI=")
                    .Append(appEUI);
            }

            if (!string.IsNullOrEmpty(devNonce))
            {
                url.Append("&DevNonce=")
                    .Append(devNonce);
            }

            var response = await client.GetAsync(url.ToString());
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

                Logger.Log(devAddr, $"error calling get device function api: {response.ReasonPhrase}, status: {response.StatusCode}, check the azure function log", LogLevel.Error);

                // TODO: FBE check if we return null or throw exception
                return new SearchDevicesResult();
            }

            var result = await response.Content.ReadAsStringAsync();
            var devices = (List<IoTHubDeviceInfo>)JsonConvert.DeserializeObject(result, typeof(List<IoTHubDeviceInfo>));
            return new SearchDevicesResult(devices);
        }

        /// <inheritdoc />
        public override async Task<SearchDevicesResult> SearchByDevEUIAsync(string devEUI)
        {
            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = new StringBuilder();
            url.Append(this.URL)
                .Append("GetDeviceByDevEUI?code=")
                .Append(this.AuthCode);

            if (!string.IsNullOrEmpty(devEUI))
            {
                url.Append("&DevEUI=")
                    .Append(devEUI);
            }

            var response = await client.GetAsync(url.ToString());
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new SearchDevicesResult();
                }

                Logger.Log(devEUI, $"error calling get device by devEUI api: {response.ReasonPhrase}, status: {response.StatusCode}, check the azure function log", LogLevel.Error);

                return new SearchDevicesResult();
            }

            var result = await response.Content.ReadAsStringAsync();
            var devices = (List<IoTHubDeviceInfo>)JsonConvert.DeserializeObject(result, typeof(List<IoTHubDeviceInfo>));
            return new SearchDevicesResult(devices);
        }
    }
}
