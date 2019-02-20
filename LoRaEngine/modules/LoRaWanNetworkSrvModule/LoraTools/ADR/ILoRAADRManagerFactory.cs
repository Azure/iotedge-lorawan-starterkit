// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    public interface ILoRAADRManagerFactory
    {
        ILoRaADRManager Create(bool isSingleGateway, ILoRaADRStrategyProvider strategyProvider);
    }
}
