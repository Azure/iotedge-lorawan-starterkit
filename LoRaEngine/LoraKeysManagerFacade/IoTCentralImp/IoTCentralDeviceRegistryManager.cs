// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.IoTCentralImp.Definitions;
    using LoRaWan;
    using Newtonsoft.Json.Linq;

    public sealed class IoTCentralDeviceRegistryManager : IDeviceRegistryManager
    {
        private const string API_VERSION = "api-version=1.1-preview";

        private readonly IDeviceProvisioningHelper provisioningHelper;
        private readonly HttpClient client;

        public IoTCentralDeviceRegistryManager(HttpClient client, IDeviceProvisioningHelper provisioningHelper)
        {
            this.client = client;
            this.provisioningHelper = provisioningHelper;
        }

        public async Task<IDevice> GetDeviceAsync(string deviceId)
        {
            var deviceRequest = await this.client.GetAsync(new Uri(this.client.BaseAddress, $"/api/devices/{deviceId}?{API_VERSION}"));

            if (deviceRequest.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            var deviceResponse = await deviceRequest.Content.ReadAsAsync<Device>();

            var provisioningResult = await this.provisioningHelper.ProvisionDevice(deviceId);

            if (provisioningResult == null)
            {
                return null;
            }

            return new IoTCentralDevice(deviceResponse, provisioningResult.Attestation, provisioningResult.AssignedIoTHubHostname);
        }

        public async Task<IDeviceTwin> GetTwinAsync(string deviceId)
        {
            var deviceRequest = await this.client.GetAsync(new Uri(this.client.BaseAddress, $"/api/devices/{deviceId}/properties?{API_VERSION}"));

            _ = deviceRequest.EnsureSuccessStatusCode();

            var properties = await deviceRequest.Content.ReadAsAsync<DesiredProperties>();

            return new DeviceTwin(deviceId, properties);
        }

        public Task CreateEdgeDeviceAsync(string edgeDeviceName, bool deployEndDevice, Uri facadeUrl, string facadeKey, string region, string resetPin, string spiSpeed, string spiDev)
        {
            throw new NotImplementedException();
        }

        public async Task<IRegistryPageResult<IDeviceTwin>> FindDeviceByAddrAsync(DevAddr devAddr)
        {
            var deviceTemplateInfos = await this.GetLoRaDeviceTemplates();
            return new DeviceTwinPageResult(this.client, deviceTemplateInfos, API_VERSION,
                item => $"SELECT $id, {item.ComponentName}.DevAddr, {item.ComponentName}.NwkSKey, {item.ComponentName}.GatewayID FROM {item.DeviceTempalteId} WHERE {item.ComponentName}.DevAddr = \"{devAddr}\" AND $simulated = false");
        }

        public Task<IRegistryPageResult<IDeviceTwin>> FindDevicesByLastUpdateDate(string updatedSince)
        {
            // At this time, there is no way to get Last update date from IoT Central
            return FindConfiguredLoRaDevices();
        }

        public async Task<IRegistryPageResult<IDeviceTwin>> FindConfiguredLoRaDevices()
        {
            var deviceTemplateInfos = await this.GetLoRaDeviceTemplates();

            return new DeviceTwinPageResult(this.client, deviceTemplateInfos, API_VERSION,
                item => $"SELECT $id, {item.ComponentName}.DevAddr, {item.ComponentName}.NwkSKey, {item.ComponentName}.GatewayID FROM {item.DeviceTempalteId} WHERE {item.ComponentName}.AppKey != \"\" AND {item.ComponentName}.AppSKey != \"\" AND {item.ComponentName}.NwkSKey != \"\" AND $simulated = false");
        }

        private async Task<IEnumerable<DeviceTemplateInfo>> GetLoRaDeviceTemplates()
        {
            var templateRequest = await this.client.GetAsync(new Uri(this.client.BaseAddress, $"/api/deviceTemplates?{API_VERSION}"));

            _ = templateRequest.EnsureSuccessStatusCode();

            var templates = await templateRequest.Content.ReadAsAsync<JObject>();

            var deviceTemplateInfos = new List<DeviceTemplateInfo>();

            foreach (var template in templates["value"].ToArray())
            {
                foreach (var item in template["capabilityModel"]["contents"]
                    .Where(item => item["schema"] != null && item["schema"].GetType() == typeof(JObject))
                    .Where(item => string.Equals(item["schema"]["@id"].ToString(), "dtmi:iotcentral:LoRaDevice;1", StringComparison.OrdinalIgnoreCase)))
                {
                    deviceTemplateInfos.Add(new DeviceTemplateInfo
                    {
                        DeviceTempalteId = template["@id"].ToString(),
                        ComponentName = item["name"].ToString()
                    });
                }
            }

            return deviceTemplateInfos;
        }
    }
}
