// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.Regions;
    using Microsoft.Azure.Devices.Shared;

    /// <summary>
    /// <see cref="LoRaDevice"/> change tracker
    /// Tracks and persists changes to the device including: frame counters, regions, preferred gateways
    /// </summary>
    internal sealed class LoRaDeviceChangeTracker : IDisposable
    {
        private LoRaDevice loRaDevice;

        public LoRaDeviceChangeTracker(LoRaDevice loRaDevice)
        {
            this.loRaDevice = loRaDevice;
        }

        public void Dispose()
        {
            _ = this.loRaDevice.SaveChangesAsync();

            GC.SuppressFinalize(this);
        }
    }
}