// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System.Threading.Tasks;

    /// <summary>
    /// Helper <see cref="ILoRaDeviceClientConnectionManager"/> implementation for unit tests
    /// </summary>
    internal class SingleDeviceConnectionManager : ILoRaDeviceClientConnectionManager
    {
        private ILoRaDeviceClient singleDeviceClient;

        public SingleDeviceConnectionManager(ILoRaDeviceClient deviceClient)
        {
            this.singleDeviceClient = deviceClient;
        }

        public bool EnsureConnected(LoRaDevice loRaDevice) => true;

        public ILoRaDeviceClient Get(LoRaDevice loRaDevice) => this.singleDeviceClient;

        public void Register(LoRaDevice loRaDevice, ILoRaDeviceClient loraDeviceClient)
        {
        }

        public void Release(LoRaDevice loRaDevice)
        {
            this.singleDeviceClient.Dispose();
        }

        public void TryScanExpiredItems()
        {
        }
    }
}