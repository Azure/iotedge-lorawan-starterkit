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
        private readonly LoRaDeviceCache deviceCache;
        private volatile bool canCache;
        private SemaphoreSlim joinLock = new SemaphoreSlim(1);
        private readonly ILogger<JoinDeviceLoader> logger;

        internal bool CanCache => this.canCache;

        internal JoinDeviceLoader(IoTHubDeviceInfo ioTHubDevice, ILoRaDeviceFactory deviceFactory, LoRaDeviceCache deviceCache, ILogger<JoinDeviceLoader> logger)
        {
            this.ioTHubDevice = ioTHubDevice;
            this.deviceFactory = deviceFactory;
            this.deviceCache = deviceCache;
            this.logger = logger;
            this.canCache = true;
        }

        internal async Task<LoRaDevice> LoadAsync()
        {
            await this.joinLock.WaitAsync();
            try
            {
                if (this.deviceCache.TryGetByDevEui(this.ioTHubDevice.DevEUI, out var cachedDevice))
                {
                    return cachedDevice;
                }
                return await this.deviceFactory.CreateAndRegisterAsync(this.ioTHubDevice, CancellationToken.None);
            }
            catch (LoRaProcessingException ex)
            {
                this.logger.LogError(ex, "join refused: error initializing OTAA device, getting twin failed");
                this.canCache = false;
                return null;
            }
            finally
            {
                _ = this.joinLock.Release();
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
