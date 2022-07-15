// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan;
    using Microsoft.Azure.Devices;

    public interface IDeviceRegistryManager
    {
        Task<IDeviceTwin> GetTwinAsync(string deviceId, CancellationToken cancellationToken);
        Task<Device> GetDeviceAsync(string deviceId);
        Task<IDeviceTwin> UpdateTwinAsync(string deviceId, string moduleId, IDeviceTwin deviceTwin, string eTag, CancellationToken cancellationToken);
        Task<IDeviceTwin> UpdateTwinAsync(string deviceId, string moduleId, IDeviceTwin deviceTwin, string eTag);
        Task<IDeviceTwin> UpdateTwinAsync(string deviceId, IDeviceTwin twin, string eTag, CancellationToken cancellationToken);
        Task<BulkRegistryOperationResult> AddDeviceWithTwinAsync(Device device, IDeviceTwin twin);
        Task<IEnumerable<IDeviceTwin>> GetEdgeDevicesAsync(CancellationToken token);
        IRegistryPageResult<IDeviceTwin> GetAllLoRaDevices();
        IRegistryPageResult<IDeviceTwin> GetLastUpdatedLoRaDevices(DateTime lastUpdateDateTime);
        IRegistryPageResult<IDeviceTwin> FindLoRaDeviceByDevAddr(DevAddr someDevAddr);
        IRegistryPageResult<string> FindLnsByNetworkId(string networkId);
        IRegistryPageResult<IDeviceTwin> FindDeviceByDevEUI(DevEui devEUI);
        Task<Device> AddDeviceAsync(Device edgeGatewayDevice);
        Task<Module> AddModuleAsync(Module moduleToAdd);
        Task ApplyConfigurationContentOnDeviceAsync(string deviceName, ConfigurationContent deviceConfigurationContent);
        Task<IDeviceTwin> GetTwinAsync(string deviceName);
        Task<Configuration> AddConfigurationAsync(Configuration configuration);
        Task<IDeviceTwin> UpdateTwinAsync(string deviceName, IDeviceTwin twin, string eTag);
        Task RemoveDeviceAsync(string deviceId);
    }
}
