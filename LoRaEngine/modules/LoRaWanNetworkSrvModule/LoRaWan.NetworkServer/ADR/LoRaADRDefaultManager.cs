// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.ADR
{
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using Microsoft.Extensions.Logging;

    public class LoRaADRDefaultManager : LoRaADRManagerBase
    {
        protected LoRaDevice LoRaDevice { get; private set; }

        private readonly ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy;

        public LoRaADRDefaultManager(ILoRaADRStore store, ILoRaADRStrategyProvider strategyProvider, ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy, LoRaDevice loRaDevice, ILogger<LoRaADRDefaultManager> logger)
            : base(store, strategyProvider, logger)
        {
            LoRaDevice = loRaDevice;
            this.frameCounterStrategy = frameCounterStrategy;
        }

        protected override void UpdateState(LoRaADRResult loRaADRResult)
        {
            if (loRaADRResult != null)
            {
                LoRaDevice.UpdatedADRProperties(loRaADRResult.DataRate, loRaADRResult.TxPower.GetValueOrDefault(), loRaADRResult.NbRepetition.GetValueOrDefault());
            }
        }

        public override Task<uint> NextFCntDown(DevEui devEUI, string gatewayId, uint clientFCntUp, uint clientFCntDown)
        {
            return this.frameCounterStrategy.NextFcntDown(LoRaDevice, clientFCntUp).AsTask();
            // update twins
        }
    }
}
