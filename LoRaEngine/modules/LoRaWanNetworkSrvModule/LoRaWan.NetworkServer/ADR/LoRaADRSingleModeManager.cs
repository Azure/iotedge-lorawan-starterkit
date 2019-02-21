// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.ADR
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using Microsoft.Extensions.Logging;

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

        protected override async Task<bool> TryUpdateState(LoRaADRResult loRaADRResult)
        {
            if (loRaADRResult != null)
            {
                this.loRaDevice.DataRate = loRaADRResult.DataRate;
                this.loRaDevice.TxPower = loRaADRResult.TxPower;
                this.loRaDevice.NbRepetition = loRaADRResult.NbRepetition;
                 // if a rate adaptation is performed we need to update local cache
                 // todo check serialization and update twin
                if (loRaADRResult.CanConfirmToDevice)
                {
                    if (!await this.loRaDevice.TrySaveADRProperties())
                    {
                        Logger.Log(this.loRaDevice.DevEUI, $"Could not save new ADR poperties on twins ", LogLevel.Error);
                        return false;
                    }
                }
            }

            return true;
        }

        public override Task<int> NextFCntDown(string devEUI, string gatewayId, int clientFCntUp, int clientFCntDown)
        {
            return this.frameCounterStrategy.NextFcntDown(this.loRaDevice, clientFCntUp).AsTask();
            // update twins
        }
    }
}
