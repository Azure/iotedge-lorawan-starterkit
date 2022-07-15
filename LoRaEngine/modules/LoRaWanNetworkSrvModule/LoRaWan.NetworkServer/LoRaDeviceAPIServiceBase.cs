// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    /// <summary>
    /// LoRa Device API contract.
    /// </summary>
    public abstract class LoRaDeviceAPIServiceBase
    {
        /// <summary>
        /// Gets URL of the API.
        /// </summary>
        public Uri URL { get; set; }

        /// <summary>
        /// Gets the authentication code for the API.
        /// </summary>
        public string AuthCode { get; private set; }

        public abstract Task<uint> NextFCntDownAsync(DevEui devEUI, uint fcntDown, uint fcntUp, string gatewayId);

        public abstract Task<bool> ABPFcntCacheResetAsync(DevEui devEUI, uint fcntUp, string gatewayId);

        /// <summary>
        /// Searchs devices based on devAddr.
        /// </summary>
        public abstract Task<SearchDevicesResult> SearchByDevAddrAsync(DevAddr devAddr);

        /// <summary>
        /// Search and locks device for join request.
        /// </summary>
        public abstract Task<SearchDevicesResult> SearchAndLockForJoinAsync(string gatewayID, DevEui devEUI, DevNonce devNonce);

        /// <summary>
        /// Searches the primary key for a station device in IoT Hub.
        /// </summary>
        /// <param name="eui">EUI of the station.</param>
        public abstract Task<string> GetPrimaryKeyByEuiAsync(StationEui eui);

        /// <summary>
        /// Fetch station credentials in IoT Hub.
        /// </summary>
        /// <param name="eui">EUI of the station.</param>
        public abstract Task<string> FetchStationCredentialsAsync(StationEui eui, ConcentratorCredentialType credentialtype, CancellationToken token);

        /// <summary>
        /// Fetch station firmware file.
        /// </summary>
        /// <param name="eui">EUI of the station.</param>
        /// <returns>An instance of <see cref="HttpContent"/> with the firmware content.</returns>
        public abstract Task<HttpContent> FetchStationFirmwareAsync(StationEui eui, CancellationToken token);

        /// <summary>
        /// Searches the primary key for a LoRa device in IoT Hub.
        /// </summary>
        /// <param name="eui">EUI of the LoRa device.</param>
        public abstract Task<string> GetPrimaryKeyByEuiAsync(DevEui eui);

        /// <summary>
        /// Sets the authorization code for the URL.
        /// </summary>
        public void SetAuthCode(string value) => AuthCode = value;

        public abstract Task SendJoinNotificationAsync(DeviceJoinNotification deviceJoinNotification, CancellationToken token);

        public abstract Task<FunctionBundlerResult> ExecuteFunctionBundlerAsync(DevEui devEUI, FunctionBundlerRequest request);

        protected LoRaDeviceAPIServiceBase()
        {
        }

        protected LoRaDeviceAPIServiceBase(NetworkServerConfiguration configuration)
        {
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));
            AuthCode = configuration.FacadeAuthCode;
            URL = configuration.FacadeServerUrl;
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
