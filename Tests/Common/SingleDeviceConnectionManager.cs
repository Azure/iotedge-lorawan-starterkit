// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;

    /// <summary>
    /// Helper <see cref="ILoRaDeviceClientConnectionManager"/> implementation for unit tests.
    /// </summary>
    public sealed class SingleDeviceConnectionManager : ILoRaDeviceClientConnectionManager
    {
        private readonly ILoRaDeviceClient singleDeviceClient;

        public SingleDeviceConnectionManager(ILoRaDeviceClient deviceClient)
        {
            this.singleDeviceClient = deviceClient;
        }

        public ILoRaDeviceClient GetClient(LoRaDevice loRaDevice) => this.singleDeviceClient;

        public void Register(LoRaDevice loRaDevice, ILoRaDeviceClient loraDeviceClient)
        {
        }

        public Task<T> UseAsync<T>(DevEui devEui, Func<ILoRaDeviceClient, Task<T>> processor)
        {
            return processor(this.singleDeviceClient);
        }

        public IAsyncDisposable ReserveConnection(DevEui devEui)
        {
            throw new NotImplementedException();
        }

        public void Release(LoRaDevice loRaDevice)
        {
            this.singleDeviceClient.Dispose();
        }

        public void Dispose() => this.singleDeviceClient.Dispose();
    }
}
