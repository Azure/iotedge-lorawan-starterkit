// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.ADR
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.ADR;

    public interface ILoRAADRManagerFactory
    {
        ILoRaADRManager Create(ILoRaADRStrategyProvider strategyProvider, ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy, LoRaDevice loRaDevice);
    }
}
