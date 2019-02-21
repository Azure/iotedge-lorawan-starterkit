// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer.ADR;

    public class FunctionBundler
    {
        private LoRaDeviceAPIServiceBase deviceApi;
        private FunctionBundlerRequest request;
        private string devEui;

        private FunctionBundler()
        {
        }

        public static FunctionBundler CreateIfRequired(
                    string gatewayId,
                    LoRaDeviceAPIServiceBase deviceApi,
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

            if (!BuildFunctionItemsToCall(loRaPayload, loRaDevice, deduplicationFactory, out FunctionBundlerItem functions))
            {
                return null;
            }

            var fcntUp = loRaPayload.GetFcnt();
            var fcntDown = loRaDevice.FCntDown;

            var bundlerRequest = new FunctionBundlerRequest
            {
                ClientFCntDown = loRaPayload.GetFcnt(),
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

            return new FunctionBundler
            {
                deviceApi = deviceApi,
                devEui = loRaDevice.DevEUI,
                request = bundlerRequest
            };
        }

        public async Task<FunctionBundlerResult> Execute()
        {
            return await this.deviceApi.FunctionBundler(this.devEui, this.request);
        }

        private static bool BuildFunctionItemsToCall(LoRaPayloadData loRaPayload, LoRaDevice loRaDevice, IDeduplicationStrategyFactory deduplicationFactory, out FunctionBundlerItem functions)
        {
            var requiresAdr = loRaPayload.IsAdrEnabled;
            var requiresExtraConfirmation = !requiresAdr && (loRaPayload.IsConfirmed || loRaPayload.IsMacAnswerRequired);

            var deduplicationStrategy = deduplicationFactory.Create(loRaDevice);
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
