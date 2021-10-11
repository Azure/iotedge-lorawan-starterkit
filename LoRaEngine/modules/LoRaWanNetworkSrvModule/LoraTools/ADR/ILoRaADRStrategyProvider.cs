// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    public interface ILoRaADRStrategyProvider
    {
        /// <summary>
        /// Strategy provider reserved for future usage.
        /// </summary>
        ILoRaADRStrategy GetStrategy();
    }
}
