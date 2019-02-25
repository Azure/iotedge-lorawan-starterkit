// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.ADR
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using Microsoft.Extensions.Logging;

    public class LoRaADRMultiGatewayManager : LoRaADRDefaultManager
    {
        private readonly LoRaDeviceAPIServiceBase deviceApi;

        public LoRaADRMultiGatewayManager(LoRaDevice loRaDevice, LoRaDeviceAPIServiceBase deviceApi)
            : base(null, null, null, loRaDevice)
        {
            Logger.Log(loRaDevice.DevEUI, "Using LoRaADRMultiGatewayManager", LogLevel.Debug);
            this.deviceApi = deviceApi;
        }

        public override Task<bool> Reset(string devEUI)
        {
            return this.deviceApi.ClearADRCache(devEUI);
        }

        public async override Task StoreADREntry(LoRaADRTableEntry newEntry)
        {
            await this.deviceApi.CalculateADRAndStoreFrame(newEntry.DevEUI, new LoRaADRRequest
            {
                FCntUp = newEntry.FCnt,
                GatewayId = newEntry.GatewayId,
                RequiredSnr = newEntry.Snr
            });

            Logger.Log(newEntry.DevEUI, "Stored ADRTableEntry", LogLevel.Debug);
        }

        public async override Task<LoRaADRResult> CalculateADRResultAndAddEntry(string devEUI, string gatewayId, int fCntUp, int fCntDown, float requiredSnr, int upstreamDataRate, int minTxPower, LoRaADRTableEntry newEntry = null)
        {
            var result = await this.deviceApi.CalculateADRAndStoreFrame(devEUI, new LoRaADRRequest
            {
                DataRate = upstreamDataRate,
                FCntDown = fCntDown,
                FCntUp = fCntUp,
                GatewayId = gatewayId,
                MinTxPowerIndex = minTxPower,
                PerformADRCalculation = true,
                RequiredSnr = requiredSnr
            });

            await this.TryUpdateState(result);
            Logger.Log(newEntry.DevEUI, $"Calculated ADR: CanConfirmToDevice: {result.CanConfirmToDevice}, TxPower: {result.TxPower}, DataRate: {result.DataRate}", LogLevel.Debug);
            return result;
        }

        protected override async Task<bool> TryUpdateState(LoRaADRResult loRaADRResult)
        {
            if (loRaADRResult != null)
            {
                if (loRaADRResult.CanConfirmToDevice && loRaADRResult.FCntDown > 0)
                {
                    this.LoRaDevice.SetFcntDown(this.LoRaDevice.FCntDown);
                }
            }

            return await base.TryUpdateState(loRaADRResult);
        }
    }
}
