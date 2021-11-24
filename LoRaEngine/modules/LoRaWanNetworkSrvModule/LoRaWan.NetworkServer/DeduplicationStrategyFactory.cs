// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using Microsoft.Extensions.Logging;
    using System;

    public class DeduplicationStrategyFactory : IDeduplicationStrategyFactory
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<DeduplicationStrategyFactory> logger;

        public DeduplicationStrategyFactory(ILoggerFactory loggerFactory, ILogger<DeduplicationStrategyFactory> logger)
        {
            this.loggerFactory = loggerFactory;
            this.logger = logger;
        }

        public ILoRaDeviceMessageDeduplicationStrategy Create(LoRaDevice loRaDevice)
        {
            if (loRaDevice is null) throw new ArgumentNullException(nameof(loRaDevice));

            if (!string.IsNullOrEmpty(loRaDevice.GatewayID))
            {
                this.logger.LogDebug("LoRa device has a specific gateway assigned. Ignoring deduplication as it is not applicable.");
                return null;
            }

            switch (loRaDevice.Deduplication)
            {
                case DeduplicationMode.Drop: return new DeduplicationStrategyDrop(this.loggerFactory.CreateLogger<DeduplicationStrategyDrop>());
                case DeduplicationMode.Mark: return new DeduplicationStrategyMark(this.loggerFactory.CreateLogger<DeduplicationStrategyMark>());
                case DeduplicationMode.None:
                default:
                {
                    this.logger.LogDebug("no Deduplication Strategy selected");
                    return null;
                }
            }
        }
    }
}
