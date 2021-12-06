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

        public ConcentratorCredentialsFunction(RegistryManager registryManager,
                                               IAzureClientFactory<BlobServiceClient> azureClientFactory)
        {
            this.registryManager = registryManager;
            this.azureClientFactory = azureClientFactory;
        }

        [FunctionName(nameof(FetchConcentratorCredentials))]
        public async Task<IActionResult> FetchConcentratorCredentials([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
                                                                      ILogger log,
                                                                      CancellationToken cancellationToken)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                log.LogError(ex, "Invalid version");
                return new BadRequestObjectResult(ex.Message);
            }

            return await RunFetchConcentratorCredentials(req, log, cancellationToken);
        }

        internal async Task<IActionResult> RunFetchConcentratorCredentials(HttpRequest req, ILogger log, CancellationToken cancellationToken)
        {
            var stationEui = req.Query["StationEui"];
            if (StringValues.IsNullOrEmpty(stationEui))
            {
                log.LogError("StationEui missing in request");
                return new BadRequestObjectResult("StationEui missing in request");
            }

            var credentialTypeQueryString = req.Query["CredentialType"];
            if (StringValues.IsNullOrEmpty(credentialTypeQueryString))
            {
                log.LogError("CredentialType missing in request");
                return new BadRequestObjectResult("CredentialType missing in request");
            }
            if (!Enum.TryParse<ConcentratorCredentialType>(credentialTypeQueryString.ToString(), out var credentialType))
            {
                log.LogError("Could not parse '{QueryString}' to a ConcentratorCredentialType.", credentialTypeQueryString.ToString());
                return new BadRequestObjectResult($"Could not parse desired concentrator credential type '{credentialTypeQueryString}'.");
            }

            var twin = await this.registryManager.GetTwinAsync(stationEui, cancellationToken);
            if (twin != null)
            {
                log.LogInformation("Retrieving '{CredentialType}' for '{StationEui}'.", credentialType.ToString(), stationEui.ToString());
                try
                {
                    var cupsProperty = (string)twin.Properties.Desired[CupsPropertyName].ToString();
                    var parsedJson = JObject.Parse(cupsProperty);
                    var url = credentialType is ConcentratorCredentialType.Lns ? parsedJson[LnsCredentialsUrlPropertyName].ToString()
                                                                               : parsedJson[CupsCredentialsUrlPropertyName].ToString();
                    var result = await GetBase64EncodedBlobAsync(url, cancellationToken);
                    return new OkObjectResult(result);
                }
                catch (Exception ex) when (ex is ArgumentOutOfRangeException or JsonReaderException)
                {
                    log.LogError("'{PropertyName}' desired property was not found or misconfigured.", CupsPropertyName);
                    return new UnprocessableEntityResult();
                }
            }
            else
            {
                log.LogInformation($"Searching for {stationEui} returned 0 devices");
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
