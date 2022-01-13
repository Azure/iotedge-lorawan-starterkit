// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class ConcentratorFirmwareFunction
    {
        internal const string CupsPropertyName = "cups";
        internal const string CupsFwUrlPropertyName = "fwUrl";
        private readonly RegistryManager registryManager;
        private readonly IAzureClientFactory<BlobServiceClient> azureClientFactory;
        private readonly ILogger<ConcentratorFirmwareFunction> logger;

        public ConcentratorFirmwareFunction(RegistryManager registryManager,
                                             IAzureClientFactory<BlobServiceClient> azureClientFactory,
                                             ILogger<ConcentratorFirmwareFunction> logger)
        {
            this.registryManager = registryManager;
            this.azureClientFactory = azureClientFactory;
            this.logger = logger;
        }

        [FunctionName(nameof(FetchConcentratorFwUpgrade))]
        public async Task<IActionResult> FetchConcentratorFwUpgrade([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
                                                                     CancellationToken cancellationToken)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                this.logger.LogError(ex, "Invalid version");
                return new BadRequestObjectResult(ex.Message);
            }

            return await RunFetchConcentratorFwUpgrade(req, cancellationToken);
        }

        internal async Task<IActionResult> RunFetchConcentratorFwUpgrade(HttpRequest req, CancellationToken cancellationToken)
        {
            if (!StationEui.TryParse((string)req.Query["StationEui"], out var stationEui))
            {
                this.logger.LogError("StationEui missing in request or invalid");
                return new BadRequestObjectResult("StationEui missing in request or invalid");
            }

            var twin = await this.registryManager.GetTwinAsync(stationEui.ToString("N", CultureInfo.InvariantCulture), cancellationToken);
            if (twin != null)
            {
                this.logger.LogDebug("Retrieving firmware upgrade URL for '{StationEui}'.", stationEui);
                try
                {
                    if (!twin.Properties.Desired.TryReadJsonBlock(CupsPropertyName, out var cupsProperty))
                        throw new ArgumentOutOfRangeException(CupsPropertyName, "Failed to read cups config");

                    var fwUrl = JObject.Parse(cupsProperty)[CupsFwUrlPropertyName].ToString();
                    var result = await GetBlobStreamAsync(fwUrl, cancellationToken);
                    return new OkObjectResult(result);
                }
                catch (Exception ex) when (ex is ArgumentOutOfRangeException or JsonReaderException)
                {
                    this.logger.LogError(ex, "Failed to parse firmware upgrade url from the '{PropertyName}' desired property.", CupsPropertyName);
                    return new UnprocessableEntityResult();
                }
            }
            else
            {
                this.logger.LogInformation($"Searching for {stationEui} returned 0 devices");
                return new NotFoundResult();
            }
        }

        internal async Task<Stream> GetBlobStreamAsync(string blobUrl, CancellationToken cancellationToken)
        {
            var blobServiceClient = this.azureClientFactory.CreateClient(FacadeStartup.WebJobsStorageClientName);
            var blobUri = new BlobUriBuilder(new Uri(blobUrl));
            return await blobServiceClient.GetBlobContainerClient(blobUri.BlobContainerName)
                                          .GetBlobClient(blobUri.BlobName)
                                          .OpenReadAsync(new BlobOpenReadOptions(false), cancellationToken);
        }
    }
}
