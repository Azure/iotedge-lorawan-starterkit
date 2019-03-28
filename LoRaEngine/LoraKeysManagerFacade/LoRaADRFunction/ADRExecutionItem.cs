// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaTools.CommonAPI;

    public class ADRExecutionItem : IFunctionBundlerExecutionItem
    {
        private readonly ILoRaADRManager adrManager;

        public ADRExecutionItem(ILoRaADRManager adrManager)
        {
            this.adrManager = adrManager;
        }

        public async Task<FunctionBundlerExecutionState> ExecuteAsync(IPipelineExecutionContext context)
        {
            context.Result.AdrResult = await this.HandleADRRequest(context.DevEUI, context.Request.AdrRequest);
            context.Result.NextFCntDown = context.Result.AdrResult != null && context.Result.AdrResult.FCntDown > 0 ? context.Result.AdrResult.FCntDown : (uint?)null;
            return FunctionBundlerExecutionState.Continue;
        }

        public int Priority => 2;

        public bool NeedsToExecute(FunctionBundlerItemType item)
        {
            return (item & FunctionBundlerItemType.ADR) == FunctionBundlerItemType.ADR;
        }

        public async Task OnAbortExecutionAsync(IPipelineExecutionContext context)
        {
            // aborts of the full pipeline indicate we do not calculate but we still want to capture the frame
            // if we have one
            context.Request.AdrRequest.PerformADRCalculation = false;
            context.Result.AdrResult = await this.HandleADRRequest(context.DevEUI, context.Request.AdrRequest);
        }

        internal async Task<LoRaADRResult> HandleADRRequest(string devEUI, LoRaADRRequest request)
        {
            if (request == null)
            {
                return null;
            }

            if (request.ClearCache)
            {
                await this.adrManager.ResetAsync(devEUI);
                return new LoRaADRResult();
            }

            var newEntry = new LoRaADRTableEntry
            {
                DevEUI = devEUI,
                FCnt = request.FCntUp,
                GatewayId = request.GatewayId,
                Snr = request.RequiredSnr
            };

            if (request.PerformADRCalculation)
            {
                return await this.adrManager.CalculateADRResultAndAddEntryAsync(devEUI, request.GatewayId, request.FCntUp, request.FCntDown, request.RequiredSnr, request.DataRate, request.MinTxPowerIndex, request.MaxDataRate, newEntry);
            }
            else
            {
                await this.adrManager.StoreADREntryAsync(newEntry);
                return await this.adrManager.GetLastResultAsync(devEUI);
            }
        }
    }
}
