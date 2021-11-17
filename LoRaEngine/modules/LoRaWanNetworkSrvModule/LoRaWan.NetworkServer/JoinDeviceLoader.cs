// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Loads devices for join requests
    /// This object remains in cache until join succeeds or timeout expires (2 minutes).
    /// </summary>
    internal class JoinDeviceLoader : IDisposable
    {
        private readonly IoTHubDeviceInfo ioTHubDevice;
        private readonly ILoRaDeviceFactory deviceFactory;
        private volatile bool canCache;
        private SemaphoreSlim joinLock = new SemaphoreSlim(1);

        internal bool CanCache => this.canCache;

        internal JoinDeviceLoader(IoTHubDeviceInfo ioTHubDevice, ILoRaDeviceFactory deviceFactory)
        {
            this.ioTHubDevice = ioTHubDevice;
            this.deviceFactory = deviceFactory;
            this.canCache = true;
        }

        internal async Task<LoRaDevice> LoadAsync()
        {
            try
            {
                await this.joinLock.WaitAsync();
                try
                {
                    return await this.deviceFactory.CreateAndRegisterAsync(this.ioTHubDevice, CancellationToken.None);
                }
                finally
                {
                    _ = this.joinLock.Release();
                }
            }
            catch (LoRaNetworkServerException ex)
            {
                Logger.Log(this.ioTHubDevice.DevEUI, $"join refused: Failed to load: {ex}", LogLevel.Error);
                this.canCache = false;
                return null;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.joinLock?.Dispose();
                this.joinLock = null;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
