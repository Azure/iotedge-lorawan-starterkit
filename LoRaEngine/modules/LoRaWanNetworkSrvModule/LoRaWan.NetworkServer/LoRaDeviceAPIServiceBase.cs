// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer.ADR;

    /// <summary>
    /// LoRa Device API contract
    /// </summary>
    public abstract class LoRaDeviceAPIServiceBase
    {
        /// <summary>
        /// Gets URL of the API
        /// </summary>
        public string URL { get; private set; }

        /// <summary>
        /// Gets the authentication code for the API
        /// </summary>
        public string AuthCode { get; private set; }

        public abstract Task<ushort> NextFCntDownAsync(string devEUI, int fcntDown, int fcntUp, string gatewayId);

        public abstract Task<bool> ABPFcntCacheResetAsync(string devEUI);

        /// <summary>
        /// Searchs devices based on devAddr
        /// </summary>
        public abstract Task<SearchDevicesResult> SearchByDevAddrAsync(string devAddr);

        /// <summary>
        /// Search and locks device for join request
        /// </summary>
        public abstract Task<SearchDevicesResult> SearchAndLockForJoinAsync(string gatewayID, string devEUI, string appEUI, string devNonce);

        /// <summary>
        /// Sets the new URL value
        /// </summary>
        public void SetURL(string value) => this.URL = value;

        /// <summary>
        /// Sets the authorization code for the URL
        /// </summary>
        public void SetAuthCode(string value) => this.AuthCode = value;

        /// <summary>
        /// Validates if the specified message from the device
        /// was already processed by any gateway in the system
        /// </summary>
        /// <param name="devEUI">Device identifier</param>
        /// <param name="fcntUp">frame count of the message we received</param>
        /// <param name="gatewayId">The current processing gateway</param>
        /// <param name="fcntDown">The frame count down of the client. This is optional and if specified
        /// will combine the framecount down checks with the message duplication check</param>
        /// <returns>true if it is a duplicate otherwise false</returns>
        public abstract Task<DeduplicationResult> CheckDuplicateMsgAsync(string devEUI, int fcntUp, string gatewayId, int? fcntDown = null);

        public abstract Task<LoRaADRResult> CalculateADRAndStoreFrame(string devEUI, LoRaADRRequest adrRequest);

        public abstract Task<FunctionBundlerResult> FunctionBundler(string devEUI, FunctionBundlerRequest request);

        protected LoRaDeviceAPIServiceBase()
        {
        }

        protected LoRaDeviceAPIServiceBase(NetworkServerConfiguration configuration)
        {
            this.AuthCode = configuration.FacadeAuthCode;
            this.URL = configuration.FacadeServerUrl;
        }

        protected static ByteArrayContent PreparePostContent(string requestBody)
        {
            var buffer = Encoding.UTF8.GetBytes(requestBody);
            var byteContent = new ByteArrayContent(buffer);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return byteContent;
        }
    }
}
