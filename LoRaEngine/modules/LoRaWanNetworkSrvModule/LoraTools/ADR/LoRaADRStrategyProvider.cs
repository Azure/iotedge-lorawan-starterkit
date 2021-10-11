// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    public class LoRaADRStrategyProvider : ILoRaADRStrategyProvider
    {
        /// <summary>
        /// Provider writtent for future strategy addition.
        /// </summary>
        public ILoRaADRStrategy GetStrategy()
        {
            return new LoRaADRStandardStrategy();
        }
    }
}
