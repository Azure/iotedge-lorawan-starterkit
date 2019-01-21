//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    /// <summary>
    /// LoRa Device API contract
    /// </summary>
    public abstract class LoRaDeviceAPIServiceBase
    {
        /// <summary>
        /// URL of the API
        /// </summary>
        public string URL { get; private set; }

        /// <summary>
        /// Authentication code for the API
        /// </summary>
        public string AuthCode { get; private set; }

        public abstract Task<ushort> NextFCntDownAsync(string devEUI, int fcntDown, int fcntUp, string gatewayId);

        public abstract Task<bool> ABPFcntCacheResetAsync(string DevEUI);

        //public abstract Task<SearchDevicesResult> SearchDevicesAsync(string gatewayId = null, string devAddr = null, string devEUI = null, string appEUI = null, string devNonce = null);
        /// <summary>
        /// Searchs devices based on devAddr
        /// </summary>
        /// <param name="devAddr"></param>
        /// <returns></returns>
        public abstract Task<SearchDevicesResult> SearchByDevAddrAsync(string devAddr);


        /// <summary>
        /// Search and locks device for join request
        /// </summary>
        /// <param name="gatewayID"></param>
        /// <param name="devEUI"></param>
        /// <param name="appEUI"></param>
        /// <param name="devNonce"></param>
        /// <returns></returns>
        public abstract Task<SearchDevicesResult> SearchAndLockForJoinAsync(string gatewayID, string devEUI, string appEUI, string devNonce);

        /// <summary>
        /// Sets the new URL value
        /// </summary>
        /// <param name="value"></param>
        public void SetURL(string value) => this.URL = value;

        /// <summary>
        /// Sets the authorization code for the URL
        /// </summary>
        /// <param name="value"></param>
        public void SetAuthCode(string value) => this.AuthCode = value;

        protected LoRaDeviceAPIServiceBase()
        {
        }

        protected LoRaDeviceAPIServiceBase(NetworkServerConfiguration configuration)
        {
            this.AuthCode = configuration.FacadeAuthCode;
            this.URL = configuration.FacadeServerUrl;
        }
    }
}
