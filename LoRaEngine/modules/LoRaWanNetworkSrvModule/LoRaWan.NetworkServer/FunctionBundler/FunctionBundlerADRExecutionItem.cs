// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.CommonAPI;
    using LoRaWan.NetworkServer.ADR;
    using Microsoft.Extensions.Logging;

    public class FunctionBundlerADRExecutionItem : IFunctionBundlerExecutionItem
    {
        public void Prepare(FunctionBundlerExecutionContext context, FunctionBundlerRequest request)
        {
            request.AdrRequest = new LoRaADRRequest
            {
                DataRate = context.Request.Region.GetDRFromFreqAndChan(context.Request.Rxpk.Datr),
                FCntDown = context.FCntDown,
                FCntUp = context.FCntUp,
                GatewayId = context.GatewayId,
                MinTxPowerIndex = context.Request.Region.TXPowertoMaxEIRP.Count - 1,
                PerformADRCalculation = context.LoRaPayload.IsAdrReq,
                RequiredSnr = (float)context.Request.Rxpk.RequiredSnr
            };

            request.FunctionItems |= FunctionBundlerItemType.ADR;
        }

        public void ProcessResult(FunctionBundlerExecutionContext context, FunctionBundlerResult result)
        {
            if (result.AdrResult != null)
            {
                if (result.AdrResult.CanConfirmToDevice && result.AdrResult.FCntDown > 0)
                {
                    context.LoRaDevice.SetFcntDown(context.LoRaDevice.FCntDown);
                }
            }
        }

        public bool RequiresExecution(FunctionBundlerExecutionContext context)
        {
            return context.LoRaPayload.IsAdrEnabled;
        }
    }
}
