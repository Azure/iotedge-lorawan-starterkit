// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;

    public interface ILoRaDeviceClientConnectionManager : IDisposable
    {
        ILoRaDeviceClient GetClient(LoRaDevice loRaDevice);

        void Release(LoRaDevice loRaDevice);

        void Register(LoRaDevice loRaDevice, ILoRaDeviceClient loraDeviceClient);

        Task<T> UseAsync<T>(DevEui devEui, Func<ILoRaDeviceClient, Task<T>> processor);

        IAsyncDisposable ReserveConnection(DevEui devEui);
    }

    public static class LoRaDeviceClientConnectionManagerExtensions
    {
        public static Task UseAsync(this ILoRaDeviceClientConnectionManager connectionManager, DevEui devEui, Func<ILoRaDeviceClient, Task> processor)
        {
            if (connectionManager == null) throw new ArgumentNullException(nameof(connectionManager));

            return connectionManager.UseAsync(devEui, async client =>
            {
                await processor(client);
                return 0;
            });
        }
    }
}
