// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.ADR
{
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using Microsoft.Extensions.Logging;

    public class LoRaADRMultiGatewayManager : LoRaADRDefaultManager
    {
        private readonly LoRaDeviceAPIServiceBase deviceApi;

        public LoRaADRMultiGatewayManager(LoRaDevice loRaDevice, LoRaDeviceAPIServiceBase deviceApi, ILogger<LoRaADRMultiGatewayManager> logger)
            : base(null, null, null, loRaDevice, logger)
        {
            this.deviceApi = deviceApi;
        }

        public override Task<bool> ResetAsync(DevEui devEUI)
        {
            // needs to be called on the function bundler
            return Task.FromResult<bool>(false);
        }

        public override Task StoreADREntryAsync(LoRaADRTableEntry newEntry)
        {
            // function bundler is executing this request
            return Task.CompletedTask;
        }

        public override Task<LoRaADRResult> CalculateADRResultAndAddEntryAsync(DevEui devEUI, string gatewayId, uint fCntUp, uint fCntDown, float requiredSnr, DataRateIndex dataRate, int minTxPower, DataRateIndex maxDr, LoRaADRTableEntry newEntry = null)
        {
            return Task.FromResult<LoRaADRResult>(null);
        }
    }
}
