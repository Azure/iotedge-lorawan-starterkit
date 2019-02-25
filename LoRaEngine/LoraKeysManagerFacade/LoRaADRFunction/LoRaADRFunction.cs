// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class LoRaADRFunction
    {
        private ILoRaADRManager adrManager;

        public LoRaADRFunction(ILoRaADRManager adrManager)
        {
            this.adrManager = adrManager;
        }

        [FunctionName("ADRFunction")]
        public async Task<IActionResult> ADRFunctionImpl(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "ADRFunction/{devEUI}")]HttpRequest req,
            ILogger log,
            string devEUI)
        {
            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            EUIValidator.ValidateDevEUI(devEUI);

            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("missing body");
            }

            var adrRequest = JsonConvert.DeserializeObject<LoRaADRRequest>(requestBody);
            var result = await this.HandleADRRequest(devEUI, adrRequest);

            return new OkObjectResult(result);
        }

        public async Task<LoRaADRResult> HandleADRRequest(string devEUI, LoRaADRRequest request)
        {
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
                return await this.adrManager.CalculateADRResultAndAddEntryAsync(devEUI, request.GatewayId, request.FCntUp, request.FCntDown, request.RequiredSnr, request.DataRate, request.MinTxPowerIndex, newEntry);
            }
            else
            {
                await this.adrManager.StoreADREntryAsync(newEntry);
                return await this.adrManager.GetLastResultAsync(devEUI);
            }
        }
    }
}
