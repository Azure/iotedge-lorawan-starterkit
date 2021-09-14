// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;

    internal sealed class IoTHubDeviceRegistryManager : IDeviceRegistryManager
    {
        private readonly RegistryManager registryManager;

        internal IoTHubDeviceRegistryManager(RegistryManager registryManager)
        {
            this.registryManager = registryManager;
        }

        public Task AddDeviceAsync(Device edgeGatewayDevice)
        {
            return this.registryManager.AddDeviceAsync(edgeGatewayDevice);
        }

        public Task AddModuleAsync(Module module)
        {
            return this.registryManager.AddModuleAsync(module);
        }

        public Task ApplyConfigurationContentOnDeviceAsync(string deviceName, ConfigurationContent spec)
        {
            return this.registryManager.ApplyConfigurationContentOnDeviceAsync(deviceName, spec);
        }

        public IQuery CreateQuery(string sqlQueryString)
        {
            return this.registryManager.CreateQuery(sqlQueryString);
        }

        public IQuery CreateQuery(string inputQuery, int pageSize)
        {
            return this.registryManager.CreateQuery(inputQuery, pageSize);
        }

        public Task<Device> GetDeviceAsync(string deviceName)
        {
            return this.registryManager.GetDeviceAsync(deviceName);
        }

        public Task<Twin> GetTwinAsync(string deviceName)
        {
            return this.registryManager.GetTwinAsync(deviceName);
        }

        public Task<Twin> UpdateTwinAsync(string deviceName, string jsonTwinPatch, string eTag)
        {
            return this.registryManager.UpdateTwinAsync(deviceName, jsonTwinPatch, eTag);
        }

        public Task<Twin> UpdateTwinAsync(string otaaDeviceId, Twin otaaEndTwin, string eTag)
        {
            return this.registryManager.UpdateTwinAsync(otaaDeviceId, otaaEndTwin, eTag);
        }

        public Task UpdateTwinAsync(string deviceName, string moduleId, Twin twin, string eTag)
        {
            return this.registryManager.UpdateTwinAsync(deviceName, moduleId, twin, eTag);
        }
    }
}
