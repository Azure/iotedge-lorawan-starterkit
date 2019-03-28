// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Loads devices for join requests
    /// This object remains in cache until join succeeds or timeout expires (2 minutes)
    /// </summary>
    internal class JoinDeviceLoader
    {
        private readonly IoTHubDeviceInfo ioTHubDevice;
        private readonly ILoRaDeviceFactory deviceFactory;
        Task<LoRaDevice> loading;
        volatile bool canCache;

        internal bool CanCache => this.canCache;

        internal JoinDeviceLoader(IoTHubDeviceInfo ioTHubDevice, ILoRaDeviceFactory deviceFactory)
        {
            this.ioTHubDevice = ioTHubDevice;
            this.deviceFactory = deviceFactory;
            this.canCache = true;
            this.loading = Task.Run(() => this.LoadAsync());
        }

        /// <summary>
        /// Returns Task containing the device loading execution.
        /// Waiting for it will suspend your thread/task until the device join is complete
        /// </summary>
        internal Task<LoRaDevice> WaitCompleteAsync() => this.loading;

        async Task<LoRaDevice> LoadAsync()
        {
            var loRaDevice = this.deviceFactory.Create(this.ioTHubDevice);

            try
            {
                if (await loRaDevice.InitializeAsync())
                {
                    return loRaDevice;
                }
                else
                {
                    // will reach here if getting twins threw an exception
                    // object is non usable, must try to read twin again
                    // for the future we could retry here
                    this.canCache = false;
                    Logger.Log(loRaDevice.DevEUI, "join refused: error initializing OTAA device, getting twin failed", LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                // will reach here if the device does not have required properties in the twin
                Logger.Log(loRaDevice.DevEUI, $"join refused: error initializing OTAA device. {ex.Message}", LogLevel.Error);
            }

            return null;
        }
    }
}