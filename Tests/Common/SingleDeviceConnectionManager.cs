// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Threading;
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

        public bool EnsureConnected(LoRaDevice loRaDevice) => true;

        public ILoRaDeviceClient GetClient(LoRaDevice loRaDevice) => this.singleDeviceClient;

        public void Register(LoRaDevice loRaDevice, ILoRaDeviceClient loraDeviceClient)
        {
        }

        public void Release(LoRaDevice loRaDevice)
        {
            this.singleDeviceClient.Dispose();
        }

        public void Dispose() => this.singleDeviceClient.Dispose();

        public void CloseConnection(LoRaDevice loRaDevice) =>
            _ = this.singleDeviceClient.Disconnect();

        public Task CloseConnectionAsync(LoRaDevice loRaDevice, CancellationToken cancellationToken) =>
            _ = this.singleDeviceClient.DisconnectAsync(cancellationToken);
    }
}
