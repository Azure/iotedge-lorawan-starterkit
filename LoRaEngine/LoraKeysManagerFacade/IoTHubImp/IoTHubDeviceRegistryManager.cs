// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTHubImp
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.IoTHubImp;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    internal sealed class IoTHubDeviceRegistryManager : IDeviceRegistryManager
    {
        private readonly RegistryManager registryManager;

        private readonly string iotHubHostname;

        internal IoTHubDeviceRegistryManager(RegistryManager registryManager, string hostName)
        {
            this.registryManager = registryManager;
            this.iotHubHostname = hostName;
        }

        public async Task CreateEdgeDeviceAsync(string edgeDeviceName, bool deployEndDevice, string facadeUrl, string facadeKey, string region, string resetPin, string spiSpeed, string spiDev)
        {
            Device edgeGatewayDevice = new Device(edgeDeviceName);
            edgeGatewayDevice.Capabilities = new DeviceCapabilities()
            {
                IotEdge = true
            };

            await this.registryManager.AddDeviceAsync(edgeGatewayDevice);

            string deviceConfigurationUrl = Environment.GetEnvironmentVariable("DEVICE_CONFIG_LOCATION");
            string json = null;

            // todo correct
            using (WebClient wc = new WebClient())
            {
                json = wc.DownloadString(deviceConfigurationUrl);
            }

            json = ReplaceJsonWithCorrectValues(region, resetPin, json, spiSpeed, spiDev);

            ConfigurationContent spec = JsonConvert.DeserializeObject<ConfigurationContent>(json);
            await this.registryManager.AddModuleAsync(new Module(edgeDeviceName, "LoRaWanNetworkSrvModule"));

            await this.registryManager.ApplyConfigurationContentOnDeviceAsync(edgeDeviceName, spec);

            Twin twin = new Twin();
            twin.Properties.Desired = new TwinCollection(@"{FacadeServerUrl:'" + facadeUrl + "',FacadeAuthCode: " + "'" + facadeKey + "'}");

            var remoteTwin = await this.registryManager.GetTwinAsync(edgeDeviceName);

            await this.registryManager.UpdateTwinAsync(edgeDeviceName, "LoRaWanNetworkSrvModule", twin, remoteTwin.ETag);

            // This section will get deployed ONLY if the user selected the "deploy end device" options.
            // Information in this if clause, is for demo purpose only and should not be used for productive workloads.
            if (deployEndDevice)
            {
                var otaaDevice = new Device(DeviceFeedConstants.OtaaDeviceId);

                await this.registryManager.AddDeviceAsync(otaaDevice);

                var otaaEndTwin = new Twin();
                otaaEndTwin.Properties.Desired = new TwinCollection(@"{AppEUI:'BE7A0000000014E2',AppKey:'8AFE71A145B253E49C3031AD068277A1',GatewayID:'',SensorDecoder:'DecoderValueSensor'}");
                var otaaRemoteTwin = await this.registryManager.GetTwinAsync(DeviceFeedConstants.OtaaDeviceId);
                await this.registryManager.UpdateTwinAsync(DeviceFeedConstants.OtaaDeviceId, otaaEndTwin, otaaRemoteTwin.ETag);

                var abpDevice = new Device(DeviceFeedConstants.AbpDeviceId);
                await this.registryManager.AddDeviceAsync(abpDevice);
                var abpTwin = new Twin();
                abpTwin.Properties.Desired = new TwinCollection(@"{AppSKey:'2B7E151628AED2A6ABF7158809CF4F3C',NwkSKey:'3B7E151628AED2A6ABF7158809CF4F3C',GatewayID:'',DevAddr:'0228B1B1',SensorDecoder:'DecoderValueSensor'}");
                var abpRemoteTwin = await this.registryManager.GetTwinAsync(DeviceFeedConstants.AbpDeviceId);
                await this.registryManager.UpdateTwinAsync(DeviceFeedConstants.AbpDeviceId, abpTwin, abpRemoteTwin.ETag);
            }
        }

        public async Task<IDevice> GetDeviceAsync(string deviceName)
        {
            var device = await this.registryManager.GetDeviceAsync(deviceName);

            if (device == null)
            {
                return null;
            }

            return new IoTHubDevice(device, this.iotHubHostname);
        }

        public async Task<IDeviceTwin> GetTwinAsync(string deviceName)
        {
            var twin = await this.registryManager.GetTwinAsync(deviceName);

            if (twin == null)
            {
                return null;
            }

            return new IoTHubDeviceTwin(twin);
        }

        public Task<IRegistryPageResult<IDeviceTwin>> FindDeviceByAddrAsync(string devAddr)
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

        private static string ReplaceJsonWithCorrectValues(string region, string resetPin, string json, string spiSpeed, string spiDev)
        {
            json = json.Replace("[$region]", region);
            json = json.Replace("[$reset_pin]", resetPin);

            if (string.Equals(spiSpeed, "8", StringComparison.CurrentCultureIgnoreCase) ||
                string.IsNullOrEmpty(spiSpeed))
            {
                // default case
                json = json.Replace("[$spi_speed]", string.Empty);
            }
            else
            {
                json = json.Replace(
                    "[$spi_speed]",
                    ",'SPI_SPEED':{'value':'2'}");
            }

            if (string.Equals(spiDev, "2", StringComparison.CurrentCultureIgnoreCase) ||
                string.IsNullOrEmpty(spiDev))
            {
                // default case
                json = json.Replace("[$spi_dev]", string.Empty);
            }
            else
            {
                json = json.Replace(
                    "[$spi_dev]",
                    ",'SPI_DEV':{'value':'1'}");
            }

            return json;
        }
    }
}
