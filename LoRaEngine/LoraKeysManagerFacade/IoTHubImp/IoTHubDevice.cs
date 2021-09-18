// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTHubImp
{
    using Microsoft.Azure.Devices;

    internal class IoTHubDevice : IDevice
    {
        private readonly Device device;

        public IoTHubDevice(Device device)
        {
            this.device = device;
        }

        public string DeviceId => this.device.Id;

        public string PrimaryKey => this.device.Authentication?.SymmetricKey?.PrimaryKey;
    }
}
