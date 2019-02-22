// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer.ADR;

    public class FunctionBundlerProvider : IFunctionBundlerProvider
    {
        private readonly LoRaDeviceAPIServiceBase deviceApi;

        public FunctionBundlerProvider(LoRaDeviceAPIServiceBase deviceApi)
        {
            this.deviceApi = deviceApi;
        }

        public FunctionBundler CreateIfRequired(
                    string gatewayId,
                    LoRaPayloadData loRaPayload,
                    LoRaDevice loRaDevice,
                    IDeduplicationStrategyFactory deduplicationFactory,
                    LoRaRequest request)
        {
            if (!string.IsNullOrEmpty(loRaDevice.GatewayID))
            {
                // single gateway mode
                return null;
            }

            var deduplicationStrategy = deduplicationFactory.Create(loRaDevice);

            if (!BuildFunctionItemsToCall(loRaPayload, loRaDevice, deduplicationStrategy, out FunctionBundlerItem functions))
            {
                return null;
            }

            var fcntUp = loRaPayload.GetFcnt();
            var fcntDown = loRaDevice.FCntDown;

            var bundlerRequest = new FunctionBundlerRequest
            {
                ClientFCntDown = fcntDown,
                ClientFCntUp = fcntUp,
                GatewayId = gatewayId,
                FunctionItems = functions
            };

            if ((functions & FunctionBundlerItem.ADR) == FunctionBundlerItem.ADR)
            {
                bundlerRequest.AdrRequest = new LoRaADRRequest
                {
                    DataRate = request.LoRaRegion.GetDRFromFreqAndChan(request.Rxpk.Datr),
                    FCntDown = fcntDown,
                    FCntUp = fcntUp,
                    GatewayId = gatewayId,
                    MinTxPowerIndex = request.LoRaRegion.TXPowertoMaxEIRP.Count - 1,
                    PerformADRCalculation = loRaPayload.IsAdrReq,
                    RequiredSnr = (float)request.Rxpk.RequiredSnr
                };
            }

            return new FunctionBundler(loRaDevice.DevEUI, this.deviceApi, bundlerRequest, deduplicationStrategy);
        }

        private static bool BuildFunctionItemsToCall(LoRaPayloadData loRaPayload, LoRaDevice loRaDevice, ILoRaDeviceMessageDeduplicationStrategy deduplicationStrategy, out FunctionBundlerItem functions)
        {
            var requiresAdr = loRaPayload.IsAdrEnabled;
            var requiresExtraConfirmation = loRaPayload.IsConfirmed || loRaPayload.IsMacAnswerRequired;

            var requiresDeduplication = deduplicationStrategy != null;

            functions = 0;
            var numberOfRequests = 0;
            if (requiresDeduplication)
            {
                functions |= FunctionBundlerItem.Deduplication;
                numberOfRequests++;
            }

            if (requiresAdr)
            {
                functions |= FunctionBundlerItem.ADR;
                numberOfRequests++;
            }

            if (requiresExtraConfirmation)
            {
                functions |= FunctionBundlerItem.FCntDown;
                numberOfRequests++;
            }

            return numberOfRequests > 1;
        }
    }
}
