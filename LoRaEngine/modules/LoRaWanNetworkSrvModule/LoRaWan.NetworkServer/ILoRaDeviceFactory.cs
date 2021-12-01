// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ILoRaDeviceFactory
    {
        /// <summary>
        /// Creates, initializes and registers a new <see cref="LoRaDevice"/>. The device is added
        /// to the local cache and initialized, if we own the device (matching gateway). The connection
        /// is added to the connection manager in any case to have a way to refresh the information and
        /// detect owner change for cached devices.
        /// </summary>
        /// <returns>A new <see cref="LoRaDevice"/></returns>
        /// <exception cref="InvalidOperationException">If the device was previously registered.</exception>
        /// <exception cref="ArgumentNullException">If the deviceInfo is null.</exception>
        /// <exception cref="ArgumentException">If the deviceInfo is incomplete.</exception>
        Task<LoRaDevice> CreateAndRegisterAsync(IoTHubDeviceInfo deviceInfo, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a device client based on the devEUI and primary key.
        /// </summary>
        /// <param name="eui">Dev EUI of the device.</param>
        /// <param name="primaryKey">Primary key of the device.</param>
        ILoRaDeviceClient CreateDeviceClient(string eui, string primaryKey);
    }
}
