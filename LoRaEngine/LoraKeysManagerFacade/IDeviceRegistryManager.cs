// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Threading.Tasks;

    public interface IDeviceRegistryManager
    {
        Task CreateEdgeDeviceAsync(string edgeDeviceName, bool deployEndDevice, Uri facadeUrl, string facadeKey, string region, string resetPin, string spiSpeed, string spiDev);

        Task<IDeviceTwin> GetTwinAsync(string deviceName);

        Task<IDevice> GetDeviceAsync(string deviceName);

        Task<IRegistryPageResult<IDeviceTwin>> FindDeviceByAddrAsync(string devAddr);

        Task<IRegistryPageResult<IDeviceTwin>> FindDevicesByLastUpdateDate(string updatedSince);

        Task<IRegistryPageResult<IDeviceTwin>> FindConfiguredLoRaDevices();
    }
}
