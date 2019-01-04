//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace LoRaWan.NetworkServer.V2
{
    /// <summary>
    /// <see cref="LoRaDevice"/> frame counter session
    /// Update the frame counter on server if changes happened
    /// </summary>
    internal sealed class LoRaDeviceFrameCounterSession : IDisposable
    {
        private LoRaDevice loRaDevice;
        private ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy;

        public LoRaDeviceFrameCounterSession(LoRaDevice loRaDevice, ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy)
        {
            this.loRaDevice = loRaDevice;
            this.frameCounterStrategy = frameCounterStrategy;
        }

        public void Dispose()
        {
            if (loRaDevice.HasFrameCountChanges)
                _ = this.frameCounterStrategy.SaveChangesAsync(loRaDevice);

            GC.SuppressFinalize(this);
        }
    }
}