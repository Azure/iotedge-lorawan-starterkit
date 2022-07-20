// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan;
    using Microsoft.Azure.Devices;

    public interface IDeviceRegistryManager
    {
        Task<IDeviceTwin> GetTwinAsync(string deviceId, CancellationToken? cancellationToken = null);
        Task<ILoRaDeviceTwin> GetLoRaDeviceTwinAsync(string deviceId, CancellationToken? cancellationToken = null);
        Task<Device> GetDeviceAsync(string deviceId);
        Task<IDeviceTwin> UpdateTwinAsync(string deviceId, string moduleId, IDeviceTwin deviceTwin, string eTag, CancellationToken cancellationToken);
        Task<IDeviceTwin> UpdateTwinAsync(string deviceId, string moduleId, IDeviceTwin deviceTwin, string eTag);
        Task<IDeviceTwin> UpdateTwinAsync(string deviceId, IDeviceTwin twin, string eTag, CancellationToken cancellationToken);
        Task<BulkRegistryOperationResult> AddDeviceWithTwinAsync(Device device, IDeviceTwin twin);
        IRegistryPageResult<IDeviceTwin> GetEdgeDevices();
        IRegistryPageResult<ILoRaDeviceTwin> GetAllLoRaDevices();
        IRegistryPageResult<ILoRaDeviceTwin> GetLastUpdatedLoRaDevices(DateTime lastUpdateDateTime);
        IRegistryPageResult<ILoRaDeviceTwin> FindLoRaDeviceByDevAddr(DevAddr someDevAddr);
        IRegistryPageResult<string> FindLnsByNetworkId(string networkId);
        IRegistryPageResult<ILoRaDeviceTwin> FindDeviceByDevEUI(DevEui devEUI);
        Task<Device> AddDeviceAsync(Device edgeGatewayDevice);
        Task<Module> AddModuleAsync(Module moduleToAdd);
        Task ApplyConfigurationContentOnDeviceAsync(string deviceName, ConfigurationContent deviceConfigurationContent);
        Task<Configuration> AddConfigurationAsync(Configuration configuration);
        Task<IDeviceTwin> UpdateTwinAsync(string deviceName, IDeviceTwin twin, string eTag);
        Task RemoveDeviceAsync(string deviceId);
    }
}
