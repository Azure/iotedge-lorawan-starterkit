// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.ADR
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.ADR;

    public class LoRaADRSingleModeManager : LoRaADRManagerBase
    {
        private readonly LoRaDevice loRaDevice;
        private readonly ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy;

        public LoRaADRSingleModeManager(ILoRaADRStore store, ILoRaADRStrategyProvider strategyProvider, ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy, LoRaDevice loRaDevice)
            : base(store, strategyProvider)
        {
            this.loRaDevice = loRaDevice;
            this.frameCounterStrategy = frameCounterStrategy;
        }

        public override Task<int> NextFCntDown(string devEUI, string gatewayId, int clientFCntUp, int clientFCntDown)
        {
            return this.frameCounterStrategy.NextFcntDown(this.loRaDevice, clientFCntUp).AsTask();
        }
    }
}
