// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using LoRaTools.CommonAPI;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class ConcentratorCredentialsFunction
    {
        internal const string CupsPropertyName = "cups";
        internal const string CupsCredentialsUrlPropertyName = "cupsCredentialUrl";
        internal const string LnsCredentialsUrlPropertyName = "tcCredentialUrl";
        private readonly RegistryManager registryManager;
        private readonly IAzureClientFactory<BlobServiceClient> azureClientFactory;
        private readonly ILogger<ConcentratorCredentialsFunction> logger;

        public ConcentratorCredentialsFunction(RegistryManager registryManager,
                                               IAzureClientFactory<BlobServiceClient> azureClientFactory,
                                               ILogger<ConcentratorCredentialsFunction> logger)
        {
            this.registryManager = registryManager;
            this.azureClientFactory = azureClientFactory;
            this.logger = logger;
        }

        [FunctionName(nameof(FetchConcentratorCredentials))]
        public async Task<IActionResult> FetchConcentratorCredentials([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
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

            return await RunFetchConcentratorCredentials(req, cancellationToken);
        }

        internal async Task<IActionResult> RunFetchConcentratorCredentials(HttpRequest req, CancellationToken cancellationToken)
        {
            if (!StationEui.TryParse((string)req.Query["StationEui"], out var stationEui))
            {
                this.logger.LogError("StationEui missing in request or invalid");
                return new BadRequestObjectResult("StationEui missing in request or invalid");
            }

            var credentialTypeQueryString = req.Query["CredentialType"];
            if (StringValues.IsNullOrEmpty(credentialTypeQueryString))
            {
                this.logger.LogError("CredentialType missing in request");
                return new BadRequestObjectResult("CredentialType missing in request");
            }
            if (!Enum.TryParse<ConcentratorCredentialType>(credentialTypeQueryString.ToString(), out var credentialType))
            {
                this.logger.LogError("Could not parse '{QueryString}' to a ConcentratorCredentialType.", credentialTypeQueryString.ToString());
                return new BadRequestObjectResult($"Could not parse desired concentrator credential type '{credentialTypeQueryString}'.");
            }

            var twin = await this.registryManager.GetTwinAsync(stationEui.ToString(), cancellationToken);
            if (twin != null)
            {
                this.logger.LogInformation("Retrieving '{CredentialType}' for '{StationEui}'.", credentialType.ToString(), stationEui);
                try
                {
                    const string cupsKey = "cups";
                    if (!twin.Properties.Desired.TryReadJsonBlock(cupsKey, out var cupsProperty))
                        throw new ArgumentOutOfRangeException(cupsKey, "failed to read cups config");

                    var parsedJson = JObject.Parse(cupsProperty);
                    var url = credentialType is ConcentratorCredentialType.Lns ? parsedJson[LnsCredentialsUrlPropertyName].ToString()
                                                                               : parsedJson[CupsCredentialsUrlPropertyName].ToString();
                    var result = await GetBase64EncodedBlobAsync(url, cancellationToken);
                    return new OkObjectResult(result);
                }
                catch (Exception ex) when (ex is ArgumentOutOfRangeException
                                              or JsonReaderException
                                              or InvalidCastException
                                              or InvalidOperationException)
                {
                    this.logger.LogError(ex, "'{PropertyName}' desired property was not found or misconfigured.", CupsPropertyName);
                    return new UnprocessableEntityResult();
                }
            }
            else
            {
                this.logger.LogInformation($"Searching for {stationEui} returned 0 devices");
                return new NotFoundResult();
            }
        }

        internal virtual async Task<string> GetBase64EncodedBlobAsync(string blobUrl, CancellationToken cancellationToken)
        {
            var blobServiceClient = this.azureClientFactory.CreateClient(FacadeStartup.WebJobsStorageClientName);
            var blobUri = new BlobUriBuilder(new Uri(blobUrl));
            using var blobStream = await blobServiceClient.GetBlobContainerClient(blobUri.BlobContainerName)
                                                          .GetBlobClient(blobUri.BlobName)
                                                          .OpenReadAsync(new BlobOpenReadOptions(false), cancellationToken);
            using var base64transform = new ToBase64Transform();
            using var base64Stream = new CryptoStream(blobStream, base64transform, CryptoStreamMode.Read);
            using var memoryStream = new MemoryStream();
            using var reader = new StreamReader(memoryStream);
            await base64Stream.CopyToAsync(memoryStream, cancellationToken);
            _ = memoryStream.Seek(0, SeekOrigin.Begin);
            return await reader.ReadToEndAsync();
        }
    }
}
