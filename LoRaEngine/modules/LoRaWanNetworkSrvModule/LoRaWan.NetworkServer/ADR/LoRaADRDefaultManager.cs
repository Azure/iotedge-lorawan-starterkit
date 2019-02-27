// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.ADR
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using Microsoft.Extensions.Logging;

    public class LoRaADRDefaultManager : LoRaADRManagerBase
    {
        protected LoRaDevice LoRaDevice { get; private set; }

        private readonly ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy;

        public LoRaADRDefaultManager(ILoRaADRStore store, ILoRaADRStrategyProvider strategyProvider, ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy, LoRaDevice loRaDevice)
            : base(store, strategyProvider)
        {
            this.LoRaDevice = loRaDevice;
            this.frameCounterStrategy = frameCounterStrategy;
        }

        protected override async Task<bool> TryUpdateState(LoRaADRResult loRaADRResult)
        {
            if (loRaADRResult != null)
            {
                this.LoRaDevice.DataRate = loRaADRResult.DataRate;
                this.LoRaDevice.TxPower = loRaADRResult.TxPower;
                this.LoRaDevice.NbRep = loRaADRResult.NbRepetition;
                 // if a rate adaptation is performed we need to update local cache
                 // todo check serialization and update twin
                if (loRaADRResult.CanConfirmToDevice)
                {
                    if (!await this.LoRaDevice.TrySaveADRPropertiesAsync())
                    {
                        Logger.Log(this.LoRaDevice.DevEUI, $"could not save new ADR poperties on twins ", LogLevel.Error);
                        return false;
                    }
                }
            }

            return true;
        }

        public override Task<int> NextFCntDown(string devEUI, string gatewayId, int clientFCntUp, int clientFCntDown)
        {
            return this.frameCounterStrategy.NextFcntDown(this.LoRaDevice, clientFCntUp).AsTask();
            // update twins
        }
    }
}
