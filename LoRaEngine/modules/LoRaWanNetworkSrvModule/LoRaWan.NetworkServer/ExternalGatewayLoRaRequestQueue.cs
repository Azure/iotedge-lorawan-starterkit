// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using Microsoft.Extensions.Logging;

    internal class ExternalGatewayLoRaRequestQueue : ILoRaDeviceRequestQueue
    {
        private readonly LoRaDevice loRaDevice;

        public ExternalGatewayLoRaRequestQueue(LoRaDevice loRaDevice)
        {
            this.loRaDevice = loRaDevice;
        }

        public void Queue(LoRaRequest request)
        {
            Logger.Log(this.loRaDevice.DevEUI, $"device is not our device, ignore message", LogLevel.Debug);
            request.NotifyFailed(this.loRaDevice, LoRaDeviceRequestFailedReason.BelongsToAnotherGateway);
        }
    }
}