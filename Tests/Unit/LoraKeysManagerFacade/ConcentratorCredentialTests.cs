// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using global::LoraKeysManagerFacade;
    using global::LoRaTools.CommonAPI;
    using LoRaTools.CommonAPI;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Moq;
    using Xunit;

    public class ConcentratorCredentialTests
    {
        private readonly Mock<RegistryManager> registryManager;
        private readonly Mock<IAzureClientFactory<BlobServiceClient>> azureClientFactory;
        private readonly ConcentratorCredentialsFunction concentratorCredential;
        private readonly Mock<ILogger> loggerMock;
        private const string RawStringContent = "hello";
        private const string Base64EncodedString = "aGVsbG8=";

        public ConcentratorCredentialTests()
        {
            this.registryManager = new Mock<RegistryManager>();
            this.azureClientFactory = new Mock<IAzureClientFactory<BlobServiceClient>>();
            this.concentratorCredential = new ConcentratorCredentialsFunction(registryManager.Object, azureClientFactory.Object);
            this.loggerMock = new Mock<ILogger>();
        }

        [Fact]
        public async Task GetBase64EncodedBlobAsync_Succeeds()
        {
            var blobBytes = Encoding.UTF8.GetBytes(RawStringContent);
            using var blobStream = new MemoryStream(blobBytes);
            SetupBlobMock(blobStream);

            var result = await this.concentratorCredential.GetBase64EncodedBlobAsync("https://storage.blob.core.windows.net/container/blobname", CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(Base64EncodedString, result);
        }

        [Theory]
        [InlineData(ConcentratorCredentialType.Lns)]
        [InlineData(ConcentratorCredentialType.Cups)]
        public async Task RunFetchConcentratorCredentials_Succeeds(ConcentratorCredentialType credentialType)
        {
            var blobBytes = Encoding.UTF8.GetBytes(RawStringContent);
            using var blobStream = new MemoryStream(blobBytes);
            SetupBlobMock(blobStream);

            // http request
            var httpRequest = new Mock<HttpRequest>();
            var queryCollection = new QueryCollection(new Dictionary<string, StringValues>()
            {
                { "StationEui", new StringValues("001122FFFEAABBCC") },
                { "CredentialType", credentialType.ToString() }
            });
            httpRequest.SetupGet(x => x.Query).Returns(queryCollection);

            // twin mock
            var twin = new Twin();
            twin.Properties.Desired = new TwinCollection(JsonUtil.Strictify(@"{'cups': {
                'cupsUri': 'https://localhost:443',
                'tcUri': 'wss://localhost:5001',
                'cupsCredCrc': 1234,
                'tcCredCrc': 5678,
                'cupsCredentialUrl': 'https://storage.blob.core.windows.net/container/blob',
                'tcCredentialUrl': 'https://storage.blob.core.windows.net/container/blob'
            }}"));
            this.registryManager.Setup(m => m.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult(twin));

            var result = await this.concentratorCredential.RunFetchConcentratorCredentials(httpRequest.Object, this.loggerMock.Object, CancellationToken.None);

            Assert.NotNull(result);
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task RunFetchConcentratorCredentials_Returns_NotFound_ForMissingTwin()
        {
            // http request
            var httpRequest = new Mock<HttpRequest>();
            var queryCollection = new QueryCollection(new Dictionary<string, StringValues>()
            {
                { "StationEui", new StringValues("001122FFFEAABBCC") },
                { "CredentialType", ConcentratorCredentialType.Cups.ToString() }
            });
            httpRequest.SetupGet(x => x.Query).Returns(queryCollection);

            // twin mock
            var twin = new Twin();
            twin.Properties.Desired = new TwinCollection(JsonUtil.Strictify(@"{'cups': {
                'cupsUri': 'https://localhost:443',
                'tcUri': 'wss://localhost:5001',
                'cupsCredCrc': 1234,
                'tcCredCrc': 5678,
                'cupsCredentialUrl': 'https://storage.blob.core.windows.net/container/blob',
                'tcCredentialUrl': 'https://storage.blob.core.windows.net/container/blob'
            }}"));
            this.registryManager.Setup(m => m.GetTwinAsync("AnotherTwin", It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult(twin));

            var result = await this.concentratorCredential.RunFetchConcentratorCredentials(httpRequest.Object, this.loggerMock.Object, CancellationToken.None);

            Assert.NotNull(result);
            Assert.IsType<NotFoundResult>(result);
        }

        [Theory]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        [InlineData(false, true, false)]
        public async Task RunFetchConcentratorCredentials_Returns_BadRequest_ForMissingQueryParams(bool stationEuiAvailable, bool credentialTypeAvailable, bool wrongCredentialType)
        {
            // http request
            var httpRequest = new Mock<HttpRequest>();
            var queryDictionary = new Dictionary<string, StringValues>();
            if (stationEuiAvailable)
            {
                queryDictionary.Add("StationEui", new StringValues("001122FFFEAABBCC"));
            }
            if (credentialTypeAvailable)
            {
                queryDictionary.Add("CredentialType", wrongCredentialType ? "wrong" : ConcentratorCredentialType.Cups.ToString());
            }
            var queryCollection = new QueryCollection(queryDictionary);
            httpRequest.SetupGet(x => x.Query).Returns(queryCollection);

            var result = await this.concentratorCredential.RunFetchConcentratorCredentials(httpRequest.Object, this.loggerMock.Object, CancellationToken.None);

            Assert.NotNull(result);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        private void SetupBlobMock(MemoryStream blobStream)
        {
            var blobServiceClient = new Mock<BlobServiceClient>();
            var blobContainerClient = new Mock<BlobContainerClient>();
            var blobContainerClientResponseMock = new Mock<Response>();

            var blobClient = new Mock<BlobClient>();
            var blobClientResponseMock = new Mock<Response>();

            blobClient.Setup(m => m.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.FromResult(blobStream as Stream));

            blobServiceClient.Setup(m => m.GetBlobContainerClient(It.IsAny<string>()))
                             .Returns(Response.FromValue(blobContainerClient.Object, blobContainerClientResponseMock.Object));

            blobContainerClient.Setup(m => m.GetBlobClient(It.IsAny<string>()))
                               .Returns(Response.FromValue(blobClient.Object, blobClientResponseMock.Object));

            this.azureClientFactory.Setup(m => m.CreateClient(FacadeStartup.WebJobsStorageClientName))
                                   .Returns(blobServiceClient.Object);
        }
    }
}
