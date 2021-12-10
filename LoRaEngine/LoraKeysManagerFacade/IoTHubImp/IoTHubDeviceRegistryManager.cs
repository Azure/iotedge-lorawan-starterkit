// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTHubImp
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using LoRaWan;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public sealed class IoTHubDeviceRegistryManager : IDeviceRegistryManager
    {
        private readonly RegistryManager registryManager;

        private readonly string iotHubHostname;

        private readonly ILogger log;

        internal IoTHubDeviceRegistryManager(RegistryManager registryManager, ILogger log, string hostName)
        {
            this.registryManager = registryManager;
            this.log = log;
            this.iotHubHostname = hostName;
        }

        public async Task CreateEdgeDeviceAsync(string edgeDeviceName, bool deployEndDevice, Uri facadeUrl, string facadeKey, string region, string resetPin, string spiSpeed, string spiDev)
        {
            var edgeGatewayDevice = new Device(edgeDeviceName)
            {
                Capabilities = new DeviceCapabilities()
                {
                    IotEdge = true
                }
            };

            _ = await this.registryManager.AddDeviceAsync(edgeGatewayDevice);
            _ = await this.registryManager.AddModuleAsync(new Module(edgeDeviceName, "LoRaWanNetworkSrvModule"));

            static async Task<ConfigurationContent> GetConfigurationContentAsync(Uri configLocation, IDictionary<string, string> tokenReplacements)
            {
                using var httpClient = new HttpClient();
                var json = await httpClient.GetStringAsync(configLocation);
                foreach (var r in tokenReplacements)
                    json = json.Replace(r.Key, r.Value, StringComparison.Ordinal);
                return JsonConvert.DeserializeObject<ConfigurationContent>(json);
            }

            var deviceConfigurationContent = await GetConfigurationContentAsync(new Uri(Environment.GetEnvironmentVariable("DEVICE_CONFIG_LOCATION")), new Dictionary<string, string>
            {
                ["[$region]"] = region,
                ["[$reset_pin]"] = resetPin,
                ["[$spi_speed]"] = string.IsNullOrEmpty(spiSpeed) || string.Equals(spiSpeed, "8", StringComparison.OrdinalIgnoreCase) ? string.Empty : ",'SPI_SPEED':{'value':'2'}",
                ["[$spi_dev]"] = string.IsNullOrEmpty(spiDev) || string.Equals(spiDev, "2", StringComparison.OrdinalIgnoreCase) ? string.Empty : ",'SPI_DEV':{'value':'1'}"
            });

            await this.registryManager.ApplyConfigurationContentOnDeviceAsync(edgeDeviceName, deviceConfigurationContent);

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOG_ANALYTICS_WORKSPACE_ID")))
            {
                log.LogDebug("Opted-in to use Azure Monitor on the edge. Deploying the observability layer.");
                // If Appinsights Key is set this means that user opted in to use Azure Monitor.
                _ = await this.registryManager.AddModuleAsync(new Module(edgeDeviceName, "IotHubMetricsCollectorModule"));
                var observabilityConfigurationContent = await GetConfigurationContentAsync(new Uri(Environment.GetEnvironmentVariable("OBSERVABILITY_CONFIG_LOCATION")), new Dictionary<string, string>
                {
                    ["[$iot_hub_resource_id]"] = Environment.GetEnvironmentVariable("IOT_HUB_RESOURCE_ID"),
                    ["[$log_analytics_workspace_id]"] = Environment.GetEnvironmentVariable("LOG_ANALYTICS_WORKSPACE_ID"),
                    ["[$log_analytics_shared_key]"] = Environment.GetEnvironmentVariable("LOG_ANALYTICS_WORKSPACE_KEY")
                });

                _ = await this.registryManager.AddConfigurationAsync(new Configuration($"obs-{Guid.NewGuid()}")
                {
                    Content = observabilityConfigurationContent,
                    TargetCondition = $"deviceId='{edgeDeviceName}'"
                });
            }

            var twin = new Twin();
            twin.Properties.Desired = new TwinCollection($"{{FacadeServerUrl:'https://{Environment.GetEnvironmentVariable("FACADE_HOST_NAME", EnvironmentVariableTarget.Process)}.azurewebsites.net/api/',FacadeAuthCode: '{facadeKey}'}}");
            var remoteTwin = await this.registryManager.GetTwinAsync(edgeDeviceName);

            _ = await this.registryManager.UpdateTwinAsync(edgeDeviceName, "LoRaWanNetworkSrvModule", twin, remoteTwin.ETag);

            // This section will get deployed ONLY if the user selected the "deploy end device" options.
            // Information in this if clause, is for demo purpose only and should not be used for productive workloads.
            if (deployEndDevice)
            {
                var otaaDevice = new Device(DeviceFeedConstants.OtaaDeviceId);

                _ = await this.registryManager.AddDeviceAsync(otaaDevice);

                var otaaEndTwin = new Twin();
                otaaEndTwin.Properties.Desired = new TwinCollection(@"{AppEUI:'BE7A0000000014E2',AppKey:'8AFE71A145B253E49C3031AD068277A1',GatewayID:'',SensorDecoder:'DecoderValueSensor'}");
                var otaaRemoteTwin = _ = await this.registryManager.GetTwinAsync(DeviceFeedConstants.OtaaDeviceId);
                _ = await this.registryManager.UpdateTwinAsync(DeviceFeedConstants.OtaaDeviceId, otaaEndTwin, otaaRemoteTwin.ETag);

                var abpDevice = new Device(DeviceFeedConstants.AbpDeviceId);
                _ = await this.registryManager.AddDeviceAsync(abpDevice);
                var abpTwin = new Twin();
                abpTwin.Properties.Desired = new TwinCollection(@"{AppSKey:'2B7E151628AED2A6ABF7158809CF4F3C',NwkSKey:'3B7E151628AED2A6ABF7158809CF4F3C',GatewayID:'',DevAddr:'0228B1B1',SensorDecoder:'DecoderValueSensor'}");
                var abpRemoteTwin = await this.registryManager.GetTwinAsync(DeviceFeedConstants.AbpDeviceId);
                _ = await this.registryManager.UpdateTwinAsync(DeviceFeedConstants.AbpDeviceId, abpTwin, abpRemoteTwin.ETag);
            }
        }

        public async Task<IDevice> GetDeviceAsync(string deviceId)
        {
            var device = await this.registryManager.GetDeviceAsync(deviceId);

            if (device == null)
            {
                return null;
            }

            return new IoTHubDevice(device, this.iotHubHostname);
        }

        public async Task<IDeviceTwin> GetTwinAsync(string deviceId)
        {
            var twin = await this.registryManager.GetTwinAsync(deviceId);

            if (twin == null)
            {
                return null;
            }

            return new IoTHubDeviceTwin(twin);
        }

        public Task<IRegistryPageResult<IDeviceTwin>> FindDeviceByAddrAsync(DevAddr devAddr)
        {
            var query = this.registryManager.CreateQuery($"SELECT * FROM devices WHERE properties.desired.DevAddr = '{devAddr}' OR properties.reported.DevAddr ='{devAddr}'");
            return Task.FromResult<IRegistryPageResult<IDeviceTwin>>(new DeviceTwinPageResult(query));
        }

        public Task<IRegistryPageResult<IDeviceTwin>> FindDevicesByLastUpdateDate(string updatedSince)
        {
            var query = this.registryManager.CreateQuery($"SELECT * FROM devices where properties.desired.$metadata.$lastUpdated >= '{updatedSince}' OR properties.reported.$metadata.DevAddr.$lastUpdated >= '{updatedSince}'");
            return Task.FromResult<IRegistryPageResult<IDeviceTwin>>(new DeviceTwinPageResult(query));
        }

        public Task<IRegistryPageResult<IDeviceTwin>> FindConfiguredLoRaDevices()
        {
            var query = this.registryManager.CreateQuery($"SELECT * FROM devices WHERE is_defined(properties.desired.AppKey) OR is_defined(properties.desired.AppSKey) OR is_defined(properties.desired.NwkSKey)");
            return Task.FromResult<IRegistryPageResult<IDeviceTwin>>(new DeviceTwinPageResult(query));
        }

        public async Task<string> GetDevicePrimaryKey(string deviceName)
        {
            var device = await this.registryManager.GetDeviceAsync(deviceName);
            return device.Authentication.SymmetricKey.PrimaryKey;
        }
    }
}
