// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using Microsoft.Extensions.Logging;

    internal class ExternalGatewayLoRaRequestQueue : ILoRaDeviceRequestQueue
    {
        private readonly LoRaDevice loRaDevice;
        private readonly ILogger<ExternalGatewayLoRaRequestQueue> logger;

        public ExternalGatewayLoRaRequestQueue(LoRaDevice loRaDevice, ILogger<ExternalGatewayLoRaRequestQueue> logger)
        {
            this.loRaDevice = loRaDevice;
            this.logger = logger;
        }

        public void Queue(LoRaRequest request)
        {
            logger.LogDebug("device is not our device, ignore message");
            request.NotifyFailed(this.loRaDevice, LoRaDeviceRequestFailedReason.BelongsToAnotherGateway);
        }
    }
}
