// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.CommonAPI;
    using LoRaWan.NetworkServer.ADR;
    using System;

    public class FunctionBundlerADRExecutionItem : IFunctionBundlerExecutionItem
    {
        public void Prepare(FunctionBundlerExecutionContext context, FunctionBundlerRequest request)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (request is null) throw new ArgumentNullException(nameof(request));

            request.AdrRequest = new LoRaADRRequest
            {
#pragma warning disable CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                DataRate = context.Request.Region.GetDRFromFreqAndChan(context.Request.Rxpk.Datr),
#pragma warning restore CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
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
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (result is null) throw new ArgumentNullException(nameof(result));

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
            if (context is null) throw new ArgumentNullException(nameof(context));
            return context.LoRaPayload.IsAdrEnabled;
        }
    }
}
