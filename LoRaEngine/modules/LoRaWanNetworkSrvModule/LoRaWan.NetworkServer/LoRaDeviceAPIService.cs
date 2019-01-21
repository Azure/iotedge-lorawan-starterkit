//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    /// <summary>
    /// LoRa Device API Service
    /// </summary>
    public sealed class LoRaDeviceAPIService : LoRaDeviceAPIServiceBase
    {
        private readonly NetworkServerConfiguration configuration;
        private readonly IServiceFacadeHttpClientProvider serviceFacadeHttpClientProvider;

        public LoRaDeviceAPIService(NetworkServerConfiguration configuration, IServiceFacadeHttpClientProvider serviceFacadeHttpClientProvider)
            : base(configuration)
        {
            this.configuration = configuration;
            this.serviceFacadeHttpClientProvider = serviceFacadeHttpClientProvider;
        }

        public override async Task<ushort> NextFCntDownAsync(string devEUI, int fcntDown, int fcntUp, string gatewayId)
        {
            Logger.Log(devEUI, $"syncing FCntDown for multigateway", Logger.LoggingLevel.Info);

            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = $"{this.URL}NextFCntDown?code={this.AuthCode}&DevEUI={devEUI}&FCntDown={fcntDown}&FCntUp={fcntUp}&GatewayId={gatewayId}";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log(devEUI, $"error calling the NextFCntDown function, check the function log. {response.ReasonPhrase}", Logger.LoggingLevel.Error);
                return 0;

            }

            string fcntDownString = await response.Content.ReadAsStringAsync();

            if (ushort.TryParse(fcntDownString, out var newFCntDown))
                return newFCntDown;

            return 0;
        }


        public override async Task<bool> ABPFcntCacheResetAsync(string devEUI)
        {
            Logger.Log(devEUI, $"ABP FCnt cache reset for multigateway", Logger.LoggingLevel.Info);
            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = $"{this.URL}NextFCntDown?code={this.AuthCode}&DevEUI={devEUI}&ABPFcntCacheReset=true";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log(devEUI, $"error calling the NextFCntDown function, check the function log, {response.ReasonPhrase}", Logger.LoggingLevel.Error);
                return false;
            }

            return true;
        }


        /// <inheritdoc />
        public sealed override Task<SearchDevicesResult> SearchAndLockForJoinAsync(string gatewayID, string devEUI, string appEUI, string devNonce)
            => SearchDevicesAsync(gatewayID: gatewayID, devEUI: devEUI, appEUI: appEUI, devNonce: devNonce);

        /// <inheritdoc />
        public sealed override Task<SearchDevicesResult> SearchByDevAddrAsync(string devAddr)
            => SearchDevicesAsync(devAddr: devAddr);


        /// <summary>
        /// Helper method that calls the API GetDevice method
        /// </summary>
        /// <param name="gatewayID"></param>
        /// <param name="devAddr"></param>
        /// <param name="devEUI"></param>
        /// <param name="appEUI"></param>
        /// <param name="devNonce"></param>
        /// <returns></returns>
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

                    if (!string.IsNullOrEmpty(badReqResult) && string.Equals(badReqResult, "UsedDevNonce", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Logger.Log(devEUI ?? string.Empty, $"DevNonce already used by this device", Logger.LoggingLevel.Info);
                        return new SearchDevicesResult
                        {
                            IsDevNonceAlreadyUsed = true,
                        };
                    }
                }
                
                Logger.Log(devAddr, $"error calling façade api: {response.ReasonPhrase}, status: {response.StatusCode}, check the azure function log", Logger.LoggingLevel.Error);

                // TODO: FBE check if we return null or throw exception
                return new SearchDevicesResult();
            }

            
            var result = await response.Content.ReadAsStringAsync();
            var devices = ((List<IoTHubDeviceInfo>)JsonConvert.DeserializeObject(result, typeof(List<IoTHubDeviceInfo>)));
            return new SearchDevicesResult(devices);
        }
    }
}
