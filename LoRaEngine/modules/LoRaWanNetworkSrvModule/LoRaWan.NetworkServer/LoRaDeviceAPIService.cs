using LoRaWan.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    /// <summary>
    /// Results of a <see cref="LoRaDeviceAPIServiceBase.SearchDevicesAsync"/> call
    /// </summary>
    public class SearchDevicesResult
    {
        /// <summary>
        /// List of devices that match the criteria
        /// </summary>
        public IReadOnlyList<IoTHubDeviceInfo> Devices { get; }

        /// <summary>
        /// Indicates dev nonce already used
        /// </summary>
        public bool IsDevNonceAlreadyUsed { get; set; }

        public SearchDevicesResult()
        {

        }

        public SearchDevicesResult(IReadOnlyList<IoTHubDeviceInfo> devices)
        {
            this.Devices = devices;
        }
    }


    /// <summary>
    /// LoRa Device API contract
    /// </summary>
    public abstract class LoRaDeviceAPIServiceBase
    {
        public abstract Task<ushort> NextFCntDownAsync(string devEUI, int fcntDown, int fcntUp, string gatewayId);

        public abstract Task<bool> ABPFcntCacheResetAsync(string DevEUI);

        public abstract Task<SearchDevicesResult> SearchDevicesAsync(string gatewayId, string devAddr = null, string devEUI = null, string appEUI = null, string devNonce = null);
    }


    /// <summary>
    /// LoRa Device API Service
    /// </summary>
    public sealed class LoRaDeviceAPIService : LoRaDeviceAPIServiceBase
    {
        public string facadeServerUrl;
        public string facadeAuthCode;
        private readonly NetworkServerConfiguration configuration;
        private readonly IServiceFacadeHttpClientProvider serviceFacadeHttpClientProvider;

        public LoRaDeviceAPIService(NetworkServerConfiguration configuration, IServiceFacadeHttpClientProvider serviceFacadeHttpClientProvider)
        {
            this.configuration = configuration;
            this.facadeAuthCode = configuration.FacadeAuthCode;
            this.facadeServerUrl = configuration.FacadeServerUrl;
            this.serviceFacadeHttpClientProvider = serviceFacadeHttpClientProvider;
        }

        public override async Task<ushort> NextFCntDownAsync(string devEUI, int fcntDown, int fcntUp, string gatewayId)
        {
            Logger.Log(devEUI, $"syncing FCntDown for multigateway", Logger.LoggingLevel.Info);

            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = $"{facadeServerUrl}NextFCntDown?code={facadeAuthCode}&DevEUI={devEUI}&FCntDown={fcntDown}&FCntUp={fcntUp}&GatewayId={gatewayId}";
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
            var url = $"{facadeServerUrl}NextFCntDown?code={facadeAuthCode}&DevEUI={devEUI}&ABPFcntCacheReset=true";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log(devEUI, $"error calling the NextFCntDown function, check the function log, {response.ReasonPhrase}", Logger.LoggingLevel.Error);
                return false;
            }

            return true;
        }

      


        public override async Task<SearchDevicesResult> SearchDevicesAsync(string gatewayId, string devAddr = null, string devEUI = null, string appEUI = null, string devNonce = null)
        {
            var client = this.serviceFacadeHttpClientProvider.GetHttpClient();
            var url = new StringBuilder();
            url.Append(facadeServerUrl)
                .Append("GetDevice?code=")
                .Append(facadeAuthCode)
                .Append("&GatewayId=")
                .Append(gatewayId);

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

                    if (!String.IsNullOrEmpty(badReqResult) && badReqResult == "UsedDevNonce")
                    {
                        Logger.Log(devEUI ?? string.Empty, $"DevNonce already used by this device", Logger.LoggingLevel.Info);
                        return new SearchDevicesResult
                        {
                            IsDevNonceAlreadyUsed = true,
                        };
                    }
                }
                
                Logger.Log(devAddr, $"error calling façade api: {response.ReasonPhrase} check the azure function log", Logger.LoggingLevel.Error);

                // TODO: FBE check if we return null or throw exception
                return new SearchDevicesResult();
            }

            
            var result = await response.Content.ReadAsStringAsync();
            var devices = ((List<IoTHubDeviceInfo>)JsonConvert.DeserializeObject(result, typeof(List<IoTHubDeviceInfo>)));
            return new SearchDevicesResult(devices);
        }
    }
}
