// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure;
    using Azure.Storage.Blobs;
    using LoRaTools;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
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

        private readonly IDeviceRegistryManager registryManager;
        private readonly IAzureClientFactory<BlobServiceClient> azureClientFactory;
        private readonly ILogger<ConcentratorFirmwareFunction> logger;

        public ConcentratorFirmwareFunction(IDeviceRegistryManager registryManager,
                                             IAzureClientFactory<BlobServiceClient> azureClientFactory,
                                             ILogger<ConcentratorFirmwareFunction> logger)
        {
            this.registryManager = registryManager;
            this.azureClientFactory = azureClientFactory;
            this.logger = logger;
        }

        [FunctionName(nameof(FetchConcentratorFirmware))]
        public async Task<IActionResult> FetchConcentratorFirmware([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
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

            return await RunFetchConcentratorFirmware(req, cancellationToken);
        }

        internal async Task<IActionResult> RunFetchConcentratorFirmware(HttpRequest req, CancellationToken cancellationToken)
        {
            if (!StationEui.TryParse((string)req.Query["StationEui"], out var stationEui))
            {
                this.logger.LogError("StationEui missing in request or invalid");
                return new BadRequestObjectResult("StationEui missing in request or invalid");
            }

            using var stationScope = this.logger.BeginEuiScope(stationEui);

            var twin = await this.registryManager.GetTwinAsync(stationEui.ToString("N", CultureInfo.InvariantCulture), cancellationToken);
            if (twin != null)
            {
                this.logger.LogDebug("Retrieving firmware url for '{StationEui}'.", stationEui);
                try
                {
                    if (!twin.Properties.Desired.TryReadJsonBlock(CupsPropertyName, out var cupsProperty))
                        throw new ArgumentOutOfRangeException(CupsPropertyName, "Failed to read CUPS config");

                    var fwUrl = JObject.Parse(cupsProperty)[CupsFwUrlPropertyName].ToString();
                    var (fwLength, stream) = await GetBlobStreamAsync(fwUrl, cancellationToken);
                    return new FileStreamWithContentLengthResult(stream, "application/octet-stream", fwLength);
                }
                catch (Exception ex) when (ex is ArgumentOutOfRangeException or JsonReaderException or NullReferenceException)
                {
                    var message = $"Failed to parse firmware upgrade url from the '{CupsPropertyName}' desired property.";
                    this.logger.LogError(ex, message);
                    return new ObjectResult(message)
                    {
                        StatusCode = (int)HttpStatusCode.InternalServerError,
                    };
                }
                catch (RequestFailedException ex) when (ExceptionFilterUtility.True(() => this.logger.LogError(ex, "Failed to download firmware from storage.")))
                {
                    return new ObjectResult("Failed to download firmware")
                    {
                        StatusCode = (int)HttpStatusCode.InternalServerError
                    };
                }
            }
            else
            {
                this.logger.LogInformation($"Searching for {stationEui} returned 0 devices");
                return new NotFoundResult();
            }
        }

        private async Task<(long, Stream)> GetBlobStreamAsync(string blobUrl, CancellationToken cancellationToken)
        {
            var blobServiceClient = this.azureClientFactory.CreateClient(FacadeStartup.WebJobsStorageClientName);
            var blobUri = new BlobUriBuilder(new Uri(blobUrl));
            var blobClient = blobServiceClient.GetBlobContainerClient(blobUri.BlobContainerName)
                                              .GetBlobClient(blobUri.BlobName);
            var blobProperties = await blobClient.GetPropertiesAsync(null, cancellationToken);
            var streamingResult = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return (blobProperties.Value.ContentLength, streamingResult.Value.Content);
        }
    }
}
