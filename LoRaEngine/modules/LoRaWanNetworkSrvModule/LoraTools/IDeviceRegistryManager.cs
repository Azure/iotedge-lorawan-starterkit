// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;

    public interface IDeviceRegistryManager : IDisposable
    {
        Task<Twin> GetTwinAsync(string deviceId, CancellationToken cancellationToken);
        Task<Device> GetDeviceAsync(string deviceId);
        Task<Twin> UpdateTwinAsync(string deviceId, string moduleId, Twin deviceTwin, string eTag, CancellationToken cancellationToken);
        Task<Twin> UpdateTwinAsync(string deviceId, string moduleId, Twin deviceTwin, string eTag);
        Task<Twin> UpdateTwinAsync(string deviceId, Twin twin, string eTag, CancellationToken cancellationToken);
        Task<BulkRegistryOperationResult> AddDeviceWithTwinAsync(Device device, Twin twin);
        IQuery CreateQuery(string query);
        IQuery CreateQuery(string query, int? pageSize);
        Task<Device> AddDeviceAsync(Device edgeGatewayDevice);
        Task<Module> AddModuleAsync(Module moduleToAdd);
        Task ApplyConfigurationContentOnDeviceAsync(string deviceName, ConfigurationContent deviceConfigurationContent);
        Task<Twin> GetTwinAsync(string deviceName);
        Task<Configuration> AddConfigurationAsync(Configuration configuration);
        Task<Twin> UpdateTwinAsync(string deviceName, Twin twin, string eTag);
        Task RemoveDeviceAsync(string deviceId);
    }
}
