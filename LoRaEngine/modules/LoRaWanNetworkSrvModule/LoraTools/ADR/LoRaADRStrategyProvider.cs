// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using Microsoft.Extensions.Logging;

    public class LoRaADRStrategyProvider : ILoRaADRStrategyProvider
    {
        private readonly ILoggerFactory loggerFactory;

        public LoRaADRStrategyProvider(ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Provider writtent for future strategy addition.
        /// </summary>
        public ILoRaADRStrategy GetStrategy()
        {
            return new LoRaADRStandardStrategy(this.loggerFactory.CreateLogger<LoRaADRStandardStrategy>());
        }
    }
}
