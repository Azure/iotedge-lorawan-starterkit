// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;

    internal class IoTCentralDeviceRegistryManager : IDeviceRegistryManager
    {
        private readonly HttpClient client;

        public IoTCentralDeviceRegistryManager(HttpClient client)
        {
            this.client = client;
        }

        public Task<IDevice> GetDeviceAsync(string deviceName)
        {
            throw new NotImplementedException();
        }

        public Task<IDeviceTwin> GetTwinAsync(string deviceName)
        {
            throw new NotImplementedException();
        }

        public Task CreateEdgeDeviceAsync(string edgeDeviceName, bool deployEndDevice, string facadeUrl, string facadeKey, string region, string resetPin, string spiSpeed, string spiDev)
        {
            throw new NotImplementedException();
        }

        public Task<IRegistryPageResult<IDeviceTwin>> FindDeviceByAddrAsync(string devAddr)
        {
            throw new NotImplementedException();
        }

        public Task<IRegistryPageResult<IDeviceTwin>> FindDevicesByLastUpdateDate(string updatedSince)
        {
            throw new NotImplementedException();
        }

        public Task<IRegistryPageResult<IDeviceTwin>> FindConfiguredLoRaDevices()
        {
            throw new NotImplementedException();
        }
    }
}
