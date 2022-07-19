// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.IoTHubImpl
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan;
    using Microsoft.Azure.Devices;

    public sealed class IoTHubRegistryManager : IDeviceRegistryManager, IDisposable
    {
        private readonly RegistryManager instance;

        public static IDeviceRegistryManager CreateWithProvider(Func<RegistryManager> registryManagerProvider)
        {
            return registryManagerProvider == null
                ? throw new ArgumentNullException(nameof(registryManagerProvider))
                : (IDeviceRegistryManager)new IoTHubRegistryManager(registryManagerProvider);
        }

        internal IoTHubRegistryManager(Func<RegistryManager> registryManagerProvider)
        {
            this.instance = registryManagerProvider() ?? throw new InvalidOperationException("RegistryManager provider provided a null RegistryManager.");
        }

        public Task<Configuration> AddConfigurationAsync(Configuration configuration) => this.instance.AddConfigurationAsync(configuration);

        public Task<Device> AddDeviceAsync(Device edgeGatewayDevice) => this.instance.AddDeviceAsync(edgeGatewayDevice);

        public Task<BulkRegistryOperationResult> AddDeviceWithTwinAsync(Device device, IDeviceTwin twin) => this.instance.AddDeviceWithTwinAsync(device, twin.ToIoTHubDeviceTwin());

        public Task<Module> AddModuleAsync(Module moduleToAdd) => this.instance.AddModuleAsync(moduleToAdd);

        public Task ApplyConfigurationContentOnDeviceAsync(string deviceName, ConfigurationContent deviceConfigurationContent)
            => this.instance.ApplyConfigurationContentOnDeviceAsync(deviceName, deviceConfigurationContent);

        public void Dispose() => this.instance?.Dispose();

        public Task<Device> GetDeviceAsync(string deviceId) => this.instance.GetDeviceAsync(deviceId);

        public async Task<IDeviceTwin> GetTwinAsync(string deviceId, CancellationToken cancellationToken)
            => new IoTHubDeviceTwin(await this.instance.GetTwinAsync(deviceId, cancellationToken));

        public async Task<IDeviceTwin> GetTwinAsync(string deviceName) => new IoTHubDeviceTwin(await this.instance.GetTwinAsync(deviceName));

        public async Task<IDeviceTwin> UpdateTwinAsync(string deviceId, string moduleId, IDeviceTwin deviceTwin, string eTag)
            => new IoTHubDeviceTwin(await this.instance.UpdateTwinAsync(deviceId, moduleId, deviceTwin.ToIoTHubDeviceTwin(), eTag));

        public async Task<IDeviceTwin> UpdateTwinAsync(string deviceId, IDeviceTwin twin, string eTag, CancellationToken cancellationToken)
            => new IoTHubDeviceTwin(await this.instance.UpdateTwinAsync(deviceId, twin.ToIoTHubDeviceTwin(), eTag, cancellationToken));

        public async Task<IDeviceTwin> UpdateTwinAsync(string deviceName, IDeviceTwin twin, string eTag)
            => new IoTHubDeviceTwin(await this.instance.UpdateTwinAsync(deviceName, twin.ToIoTHubDeviceTwin(), eTag));

        public async Task<IDeviceTwin> UpdateTwinAsync(string deviceId, string moduleId, IDeviceTwin deviceTwin, string eTag, CancellationToken cancellationToken)
            => new IoTHubDeviceTwin(await this.instance.UpdateTwinAsync(deviceId, moduleId, deviceTwin.ToIoTHubDeviceTwin(), eTag, cancellationToken));

        public Task RemoveDeviceAsync(string deviceId)
            => this.instance.RemoveDeviceAsync(deviceId);

        public IRegistryPageResult<IDeviceTwin> GetEdgeDevices()
        {
            var q = this.instance.CreateQuery($"SELECT * FROM devices.modules where moduleId = '{LoRaToolsConstants.NetworkServerModuleId}'");
            return new IoTHubDeviceTwinPageResult(q);
        }

        public IRegistryPageResult<ILoRaDeviceTwin> GetAllLoRaDevices()
        {
            var q = this.instance.CreateQuery("SELECT * FROM devices WHERE is_defined(properties.desired.AppKey) OR is_defined(properties.desired.AppSKey) OR is_defined(properties.desired.NwkSKey)");
            return new IoTHubLoRaDeviceTwinPageResult(q);
        }

        public IRegistryPageResult<ILoRaDeviceTwin> GetLastUpdatedLoRaDevices(DateTime lastUpdateDateTime)
        {
            var formattedDateTime = lastUpdateDateTime.ToString(LoRaToolsConstants.RoundTripDateTimeStringFormat, CultureInfo.InvariantCulture);
            var q = this.instance.CreateQuery($"SELECT * FROM devices where properties.desired.$metadata.$lastUpdated >= '{formattedDateTime}' OR properties.reported.$metadata.DevAddr.$lastUpdated >= '{formattedDateTime}'");
            return new IoTHubLoRaDeviceTwinPageResult(q);
        }

        public IRegistryPageResult<ILoRaDeviceTwin> FindLoRaDeviceByDevAddr(DevAddr someDevAddr)
        {
            var q = this.instance.CreateQuery($"SELECT * FROM devices WHERE properties.desired.DevAddr = '{someDevAddr}' OR properties.reported.DevAddr ='{someDevAddr}'", 100);
            return new IoTHubLoRaDeviceTwinPageResult(q);
        }

        public IRegistryPageResult<string> FindLnsByNetworkId(string networkId)
        {
            var q = this.instance.CreateQuery($"SELECT properties.desired.hostAddress, deviceId FROM devices.modules WHERE tags.network = '{networkId}'");
            return new JsonPageResult(q);
        }

        public IRegistryPageResult<ILoRaDeviceTwin> FindDeviceByDevEUI(DevEui devEUI)
        {
            var q = this.instance.CreateQuery($"SELECT * FROM devices WHERE deviceId = '{devEUI}'", 1);
            return new IoTHubLoRaDeviceTwinPageResult(q);
        }

        public async Task<ILoRaDeviceTwin> GetLoRaDeviceTwinAsync(string deviceId, CancellationToken? cancellationToken = null)
             => new IoTHubLoRaDeviceTwin(await this.instance.GetTwinAsync(deviceId, cancellationToken ?? CancellationToken.None));

        public async Task<IDeviceTwin> GetTwinAsync(string deviceId, CancellationToken? cancellationToken = null)
             => new IoTHubDeviceTwin(await this.instance.GetTwinAsync(deviceId, cancellationToken ?? CancellationToken.None));
    }
}
