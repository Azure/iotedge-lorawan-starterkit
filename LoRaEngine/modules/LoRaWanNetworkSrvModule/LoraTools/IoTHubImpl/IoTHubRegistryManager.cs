// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.IoTHubImpl
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
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

        public IQuery CreateQuery(string query) => this.instance.CreateQuery(query);

        public IQuery CreateQuery(string query, int? pageSize) => this.instance.CreateQuery(query, pageSize);

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
    }
}
