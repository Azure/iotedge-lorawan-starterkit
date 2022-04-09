// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;

    public sealed class IoTHubRegistryManager : IDeviceRegistryManager, IDisposable
    {
        private readonly RegistryManager instance;

        public static IDeviceRegistryManager CreateWithProvider(Func<RegistryManager> registryManagerProvider)
        {
            return registryManagerProvider == null
                ? throw new ArgumentNullException(nameof(registryManagerProvider))
                : (IDeviceRegistryManager)new IoTHubRegistryManager(registryManagerProvider);
        }

        private IoTHubRegistryManager(Func<RegistryManager> registryManagerProvider)
        {
            this.instance = registryManagerProvider() ?? throw new InvalidOperationException("RegistryManager provider provided a null RegistryManager.");
        }

        public Task<Configuration> AddConfigurationAsync(Configuration configuration) => this.instance.AddConfigurationAsync(configuration);

        public Task<Device> AddDeviceAsync(Device edgeGatewayDevice) => this.instance.AddDeviceAsync(edgeGatewayDevice);

        public Task<BulkRegistryOperationResult> AddDeviceWithTwinAsync(Device device, Twin twin) => this.instance.AddDeviceWithTwinAsync(device, twin);

        public Task<Module> AddModuleAsync(Module moduleToAdd) => this.instance.AddModuleAsync(moduleToAdd);

        public Task ApplyConfigurationContentOnDeviceAsync(string deviceName, ConfigurationContent deviceConfigurationContent)
            => this.instance.ApplyConfigurationContentOnDeviceAsync(deviceName, deviceConfigurationContent);

        public IQuery CreateQuery(string query) => this.instance.CreateQuery(query);

        public IQuery CreateQuery(string query, int? pageSize) => this.instance.CreateQuery(query, pageSize);

        public void Dispose() => this.instance?.Dispose();

        public Task<Device> GetDeviceAsync(string deviceId) => this.instance.GetDeviceAsync(deviceId);

        public Task<Twin> GetTwinAsync(string deviceId, CancellationToken cancellationToken)
            => this.instance.GetTwinAsync(deviceId, cancellationToken);

        public Task<Twin> GetTwinAsync(string deviceName) => this.instance.GetTwinAsync(deviceName);

        public Task<Twin> UpdateTwinAsync(string deviceId, string moduleId, Twin deviceTwin, string eTag)
            => this.instance.UpdateTwinAsync(deviceId, moduleId, deviceTwin, eTag);

        public Task<Twin> UpdateTwinAsync(string deviceId, Twin twin, string eTag, CancellationToken cancellationToken)
            => this.instance.UpdateTwinAsync(deviceId, twin, eTag, cancellationToken);

        public Task<Twin> UpdateTwinAsync(string deviceName, Twin twin, string eTag)
            => this.instance.UpdateTwinAsync(deviceName, twin, eTag);

        public Task<Twin> UpdateTwinAsync(string deviceId, string moduleId, Twin deviceTwin, string eTag, CancellationToken cancellationToken)
            => this.instance.UpdateTwinAsync(deviceId, moduleId, deviceTwin, eTag, cancellationToken);

        public Task RemoveDeviceAsync(string deviceId)
            => this.instance.RemoveDeviceAsync(deviceId);
    }
}
