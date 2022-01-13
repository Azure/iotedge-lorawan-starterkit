// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Azure;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using global::LoraKeysManagerFacade;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Primitives;
    using Moq;
    using Xunit;

    public class ConcentratorFirmwareFunctionTests
    {
        private const string BlobContent = "testcontent";

        private readonly Mock<RegistryManager> registryManager;
        private readonly Mock<IAzureClientFactory<BlobServiceClient>> azureClientFactory;
        private readonly Mock<BlobClient> blobClient;
        private readonly ConcentratorFirmwareFunction concentratorFirmware;

        private readonly StationEui TestStationEui = StationEui.Parse("11-11-11-11-11-11-11-11");

        public ConcentratorFirmwareFunctionTests()
        {
            this.registryManager = new Mock<RegistryManager>();
            this.azureClientFactory = new Mock<IAzureClientFactory<BlobServiceClient>>();
            this.blobClient = new Mock<BlobClient>();
            this.concentratorFirmware = new ConcentratorFirmwareFunction(registryManager.Object, azureClientFactory.Object, NullLogger<ConcentratorFirmwareFunction>.Instance);

            var blobServiceClient = new Mock<BlobServiceClient>();
            var blobContainerClient = new Mock<BlobContainerClient>();
            var blobContainerClientResponseMock = new Mock<Response>();
            var blobClientResponseMock = new Mock<Response>();

            blobContainerClient.Setup(m => m.GetBlobClient(It.IsAny<string>()))
                               .Returns(Response.FromValue(this.blobClient.Object, blobClientResponseMock.Object));

            blobServiceClient.Setup(m => m.GetBlobContainerClient(It.IsAny<string>()))
                             .Returns(Response.FromValue(blobContainerClient.Object, blobContainerClientResponseMock.Object));

            this.azureClientFactory.Setup(m => m.CreateClient(FacadeStartup.WebJobsStorageClientName))
                                   .Returns(blobServiceClient.Object);

            var blobBytes = Encoding.UTF8.GetBytes(BlobContent);
            using var blobStream = new MemoryStream(blobBytes);
            this.blobClient.Setup(m => m.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.FromResult(blobStream as Stream));
        }

        [Fact]
        public async Task RunFetchConcentratorFirmware_Succeeds()
        {
            var httpRequest = new Mock<HttpRequest>();
            var queryCollection = new QueryCollection(new Dictionary<string, StringValues>()
            {
                { "StationEui", new StringValues(this.TestStationEui.ToString()) }
            });
            httpRequest.SetupGet(x => x.Query).Returns(queryCollection);

            var twin = new Twin();
            twin.Properties.Desired = new TwinCollection(JsonUtil.Strictify(@"{'cups': {
                'package': '1.0.1',
                'fwUrl': 'https://storage.blob.core.windows.net/container/blob',
                'fwKeyChecksum': 123456,
                'fwSignature': '123'
            }}"));
            this.registryManager.Setup(m => m.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult(twin));

            var actual = await this.concentratorFirmware.RunFetchConcentratorFirmware(httpRequest.Object, CancellationToken.None);

            Assert.NotNull(actual);
            Assert.IsType<OkObjectResult>(actual);
        }

        [Fact]
        public async Task RunFetchConcentratorFirmware_Returns_NotFound_ForMissingTwin()
        {
            // http request
            var httpRequest = new Mock<HttpRequest>();
            var queryCollection = new QueryCollection(new Dictionary<string, StringValues>()
            {
                { "StationEui", new StringValues(this.TestStationEui.ToString()) }
            });
            httpRequest.SetupGet(x => x.Query).Returns(queryCollection);

            // twin mock
            var twin = new Twin();
            twin.Properties.Desired = new TwinCollection(JsonUtil.Strictify(@"{'cups': {
                'package': '1.0.1',
                'fwUrl': 'https://storage.blob.core.windows.net/container/blob',
                'fwKeyChecksum': 123456,
                'fwSignature': '123'
            }}"));
            this.registryManager.Setup(m => m.GetTwinAsync("AnotherTwin", It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult(twin));

            var result = await this.concentratorFirmware.RunFetchConcentratorFirmware(httpRequest.Object, CancellationToken.None);

            Assert.NotNull(result);
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
