// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Collections.Generic;
    using LoRaTools.LoRaMessage;
    using Microsoft.Extensions.Logging;

    public class FunctionBundlerProvider : IFunctionBundlerProvider
    {
        private readonly LoRaDeviceAPIServiceBase deviceApi;

        private static List<IFunctionBundlerExecutionItem> functionItems = new List<IFunctionBundlerExecutionItem>
        {
            new FunctionBundlerDeduplicationExecutionItem(),
            new FunctionBundlerADRExecutionItem(),
            new FunctionBundlerFCntDownExecutionItem(),
            new FunctionBundlerPreferredGatewayExecutionItem(),
        };

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

            var context = new FunctionBundlerExecutionContext
            {
                DeduplicationFactory = deduplicationFactory,
                FCntDown = loRaDevice.FCntDown,
                FCntUp = loRaPayload.GetFcnt(),
                GatewayId = gatewayId,
                LoRaDevice = loRaDevice,
                LoRaPayload = loRaPayload,
                Request = request
            };

            var qualifyingExecutionItems = new List<IFunctionBundlerExecutionItem>(functionItems.Count);
            for (var i = 0; i < functionItems.Count; i++)
            {
                var itm = functionItems[i];
                if (itm.RequiresExecution(context))
                {
                    qualifyingExecutionItems.Add(itm);
                }
            }

            if (qualifyingExecutionItems.Count == 0)
            {
                return null;
            }

            var bundlerRequest = new FunctionBundlerRequest
            {
                ClientFCntDown = context.FCntDown,
                ClientFCntUp = context.FCntUp,
                GatewayId = gatewayId,
                Rssi = context.Request.Rxpk.Rssi,
            };

            for (var i = 0; i < qualifyingExecutionItems.Count; i++)
            {
                qualifyingExecutionItems[i].Prepare(context, bundlerRequest);
            }

            Logger.Log(loRaDevice.DevEUI, "FunctionBundler request: ", bundlerRequest, LogLevel.Debug);

            return new FunctionBundler(loRaDevice.DevEUI, this.deviceApi, bundlerRequest, qualifyingExecutionItems, context);
        }
    }
}
