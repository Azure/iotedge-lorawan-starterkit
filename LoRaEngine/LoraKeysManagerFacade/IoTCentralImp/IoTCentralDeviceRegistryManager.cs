// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Formatting;
    using System.Security;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.IoTCentralImp.Definitions;
    using Newtonsoft.Json.Serialization;

    internal class IoTCentralDeviceRegistryManager : IDeviceRegistryManager
    {
        private const string API_VERSION = "api-version=1.0";

        private readonly IDeviceProvisioningHelper provisioningHelper;
        private readonly HttpClient client;
        private readonly JsonMediaTypeFormatter formatter;

        public IoTCentralDeviceRegistryManager(HttpClient client, IDeviceProvisioningHelper provisioningHelper)
        {
            this.provisioningHelper = provisioningHelper;
            this.client = client;
            this.provisioningHelper = provisioningHelper;
            this.formatter = new JsonMediaTypeFormatter
            {
                SerializerSettings = new Newtonsoft.Json.JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                }
            };
        }

        public async Task<IDevice> GetDeviceAsync(string deviceId)
        {
            var deviceRequest = await this.client.GetAsync($"/api/devices/{deviceId}?{API_VERSION}");

            if (deviceRequest.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            var deviceResponse = await deviceRequest.Content.ReadAsAsync<Device>();

            var attestation = this.provisioningHelper.ProvisionDevice(deviceId, out string assignedIoTHubHostname);

            return new IoTCentralDevice(deviceResponse, attestation, assignedIoTHubHostname);
        }

        public async Task<IDeviceTwin> GetTwinAsync(string deviceId)
        {
            var deviceRequest = await this.client.GetAsync($"/api/devices/{deviceId}/properties?{API_VERSION}");

            deviceRequest.EnsureSuccessStatusCode();

            var properties = await deviceRequest.Content.ReadAsAsync<DesiredProperties>();

            return new DeviceTwin(deviceId, properties);
        }

        public Task CreateEdgeDeviceAsync(string edgeDeviceName, bool deployEndDevice, string facadeUrl, string facadeKey, string region, string resetPin, string spiSpeed, string spiDev)
        {
            throw new NotImplementedException();
        }

        public Task<IRegistryPageResult<IDeviceTwin>> FindDeviceByAddrAsync(string devAddr)
        {
            var pageResult = new DeviceTwinPageResult(this.client, API_VERSION, c => c.GetDevAddr() == devAddr);
            return Task.FromResult<IRegistryPageResult<IDeviceTwin>>(pageResult);
        }

        public Task<IRegistryPageResult<IDeviceTwin>> FindDevicesByLastUpdateDate(string updatedSince)
        {
            var referenceDate = DateTime.Parse(updatedSince);
            var pageResult = new DeviceTwinPageResult(this.client, API_VERSION, c => c.GetLastUpdated() >= referenceDate);

            return Task.FromResult<IRegistryPageResult<IDeviceTwin>>(pageResult);
        }

        public Task<IRegistryPageResult<IDeviceTwin>> FindConfiguredLoRaDevices()
        {
            var pageResult = new DeviceTwinPageResult(this.client, API_VERSION, c => c.GetDevAddr() != null && c.GetNwkSKey() != null);
            return Task.FromResult<IRegistryPageResult<IDeviceTwin>>(pageResult);
        }
    }
}
