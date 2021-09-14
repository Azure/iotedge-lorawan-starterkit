// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;

    public interface IDeviceRegistryManager
    {
        Task AddDeviceAsync(Device edgeGatewayDevice);

        Task AddModuleAsync(Module module);

        Task ApplyConfigurationContentOnDeviceAsync(string deviceName, ConfigurationContent spec);

        Task<Twin> GetTwinAsync(string deviceName);

        Task<Twin> UpdateTwinAsync(string deviceName, string jsonTwinPatch, string eTag);

        Task<Twin> UpdateTwinAsync(string otaaDeviceId, Twin otaaEndTwin, string eTag);

        Task UpdateTwinAsync(string deviceName, string moduleId, Twin twin, string eTag);

        Task<Device> GetDeviceAsync(string deviceName);

        IQuery CreateQuery(string inputQuery);

        IQuery CreateQuery(string inputQuery, int pageSize);
    }
}
