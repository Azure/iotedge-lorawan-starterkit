// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Buffers;
    using System.Buffers.Binary;
    using System.IO;
    using System.IO.Pipelines;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using global::LoRaTools.CommonAPI;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.NetworkServer.BasicsStation.Processors;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public class CupsProtocolMessageProcessorTests
    {
        private readonly Mock<IBasicsStationConfigurationService> basicsStationConfigurationService;
        private readonly Mock<LoRaDeviceAPIServiceBase> deviceAPIServiceBase;
        private readonly CupsProtocolMessageProcessor processor;

        private const string StationEui = "aaaa:bbff:fecc:dddd";
        private const string CupsUri = "https://localhost:443";
        private const string TcUri = "wss://localhost:5001";
        private const uint CredentialsChecksum = 12345;

        public CupsProtocolMessageProcessorTests()
        {
            this.basicsStationConfigurationService = new Mock<IBasicsStationConfigurationService>();
            this.deviceAPIServiceBase = new Mock<LoRaDeviceAPIServiceBase>();
            var logger = new Mock<ILogger<CupsProtocolMessageProcessor>>();
            this.processor = new CupsProtocolMessageProcessor(this.basicsStationConfigurationService.Object,
                                                              this.deviceAPIServiceBase.Object,
                                                              logger.Object);
        }

        [Fact]
        public async Task HandleUpdateInfoAsync_Succeeds_WithNoUpdates()
        {
            // setup valid http context
            using var receivedResponse = MemoryPool<byte>.Shared.Rent();
            SetupValidHttpContextWithRequest(out var httpContext, receivedResponse.Memory);

            // setting up the twin in such a way that there are no updates
            var cupsTwinInfo = new CupsTwinInfo(new Uri(CupsUri),
                                                new Uri(TcUri),
                                                CredentialsChecksum,
                                                CredentialsChecksum);
            _ = this.basicsStationConfigurationService.Setup(m => m.GetCupsConfigAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                                      .Returns(Task.FromResult(cupsTwinInfo));

            // Act
            await this.processor.HandleUpdateInfoAsync(httpContext.Object, default);

            // Assert
            var responseLength = httpContext.Object.Response.ContentLength;
            Assert.NotNull(responseLength);
            var responseBytes = receivedResponse.Memory.Span[..(int)responseLength].ToArray();
            Assert.True(responseBytes.All(b => b == 0));
        }

        [Fact]
        public async Task HandleUpdateInfoAsync_Succeeds_WithCupsUriUpdates()
        {
            // setup valid http context
            using var receivedResponse = MemoryPool<byte>.Shared.Rent();
            SetupValidHttpContextWithRequest(out var httpContext, receivedResponse.Memory);

            // setting up the twin in such a way that there are no updates
            var anotherCupsUri = "https://anotheruri:443";
            var cupsTwinInfo = new CupsTwinInfo(new Uri(anotherCupsUri),
                                                new Uri(TcUri),
                                                CredentialsChecksum,
                                                CredentialsChecksum);
            _ = this.basicsStationConfigurationService.Setup(m => m.GetCupsConfigAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                                      .Returns(Task.FromResult(cupsTwinInfo));

            // Act
            await this.processor.HandleUpdateInfoAsync(httpContext.Object, default);

            // Assert
            var responseLength = httpContext.Object.Response.ContentLength;
            Assert.NotNull(responseLength);
            var responseBytes = receivedResponse.Memory.Span[..(int)responseLength].ToArray();
            Assert.Equal(anotherCupsUri.Length, responseBytes[0]);
            Assert.Equal(anotherCupsUri, Encoding.UTF8.GetString(responseBytes.Slice(1, anotherCupsUri.Length)));
            // asserting all other bytes are 0 as there are no further updates
            Assert.True(responseBytes[(anotherCupsUri.Length + 1)..].All(b => b == 0));
        }


        [Fact]
        public async Task HandleUpdateInfoAsync_Succeeds_WithTcUriUpdates()
        {
            // setup valid http context
            using var receivedResponse = MemoryPool<byte>.Shared.Rent();
            SetupValidHttpContextWithRequest(out var httpContext, receivedResponse.Memory);

            // setting up the twin in such a way that there are no updates
            var anotherTcUri = "wss://anotheruri:5001";
            var cupsTwinInfo = new CupsTwinInfo(new Uri(CupsUri),
                                                new Uri(anotherTcUri),
                                                CredentialsChecksum,
                                                CredentialsChecksum);
            _ = this.basicsStationConfigurationService.Setup(m => m.GetCupsConfigAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                                      .Returns(Task.FromResult(cupsTwinInfo));

            // Act
            await this.processor.HandleUpdateInfoAsync(httpContext.Object, default);

            // Assert
            var responseLength = httpContext.Object.Response.ContentLength;
            Assert.NotNull(responseLength);
            var responseBytes = receivedResponse.Memory.Span[..(int)responseLength].ToArray();
            Assert.Equal(anotherTcUri.Length, responseBytes[1]);
            Assert.Equal(anotherTcUri, Encoding.UTF8.GetString(responseBytes.Slice(2, anotherTcUri.Length)));
            // asserting other bytes are 0 as there are no further updates
            Assert.True(responseBytes[0] == 0);
            Assert.True(responseBytes[(anotherTcUri.Length + 2)..].All(b => b == 0));
        }

        [Fact]
        public async Task HandleUpdateInfoAsync_Succeeds_WithCredentialUpdates()
        {
            // setup valid http context
            using var receivedResponse = MemoryPool<byte>.Shared.Rent();
            SetupValidHttpContextWithRequest(out var httpContext, receivedResponse.Memory);

            // setting up the twin in such a way that there are no updates
            uint anotherChecksum = 56789;
            var credentialBytes = new byte[] { 10, 11, 12, 13 };
            var cupsTwinInfo = new CupsTwinInfo(new Uri(CupsUri),
                                                new Uri(TcUri),
                                                anotherChecksum,
                                                anotherChecksum);
            _ = this.deviceAPIServiceBase.Setup(m => m.FetchStationCredentialsAsync(It.IsAny<StationEui>(), It.IsAny<ConcentratorCredentialType>(), It.IsAny<CancellationToken>()))
                                         .Returns(Task.FromResult(Convert.ToBase64String(credentialBytes)));
            _ = this.basicsStationConfigurationService.Setup(m => m.GetCupsConfigAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                                      .Returns(Task.FromResult(cupsTwinInfo));

            // Act
            await this.processor.HandleUpdateInfoAsync(httpContext.Object, default);

            // Assert
            var responseLength = httpContext.Object.Response.ContentLength;
            Assert.NotNull(responseLength);
            var responseBytes = receivedResponse.Memory.Span[..(int)responseLength].ToArray();
            // Cups Credentials
            // responseBytes[0] is 0 because no cups uri updates
            // responseBytes[1] is 0 because no tc uri updates
            // responseBytes[2] and responseBytes[3] contain the int16 in little endian for cups credential length
            // responseBytes[4..4+CredentialsLength-1] contain the cups credential bytes
            Assert.Equal(credentialBytes.Length, BinaryPrimitives.ReadInt16LittleEndian(responseBytes.Slice(2, 2)));
            Assert.Equal(credentialBytes, responseBytes.Slice(4, credentialBytes.Length));
            // Tc Credentials
            // responseBytes[4+CredentialsLength] and responseBytes[4+CredentialsLength+1] contain the int16 in little endian for tc credential length
            // responseBytes[4+CredentialsLength+2..4+CredentialsLength+2-1] contain the tc credential bytes
            Assert.Equal(credentialBytes.Length, BinaryPrimitives.ReadInt16LittleEndian(responseBytes.Slice(4 + credentialBytes.Length, 2)));
            Assert.Equal(credentialBytes, responseBytes.Slice(4 + credentialBytes.Length + 2, credentialBytes.Length));

            // asserting other fields are 0 as there are no further updates
            Assert.True(responseBytes[0] == 0);
            Assert.True(responseBytes[1] == 0);
            Assert.True(responseBytes[((2 * credentialBytes.Length) + 6)..].All(b => b == 0));
        }

        private static void SetupValidHttpContextWithRequest(out Mock<HttpContext> httpContext, Memory<byte> receivedResponse)
        {
            httpContext = new Mock<HttpContext>();
            var httpRequest = new Mock<HttpRequest>();
            _ = httpRequest.Setup(r => r.Body).Returns(GetRequestStream());
            _ = httpContext.Setup(m => m.Request).Returns(httpRequest.Object);
            var httpResponse = new Mock<HttpResponse>();
            var bodyWriter = new Mock<PipeWriter>();
            _ = bodyWriter.Setup(m => m.WriteAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
                      .Callback<ReadOnlyMemory<byte>, CancellationToken>((memoryPortion, _) =>
                      {
                          memoryPortion.CopyTo(receivedResponse);
                      });
            _ = httpResponse.SetupProperty(m => m.ContentType);
            _ = httpResponse.SetupProperty(m => m.ContentLength);
            _ = httpResponse.Setup(m => m.BodyWriter).Returns(bodyWriter.Object);
            _ = httpContext.Setup(m => m.Response).Returns(httpResponse.Object);
        }

        private static Stream GetRequestStream()
        {
            var cupsRequest = JsonUtil.Strictify(@$"{{'router':'{StationEui}','cupsUri':'{CupsUri}',
                                                      'tcUri':'{TcUri}','cupsCredCrc':{CredentialsChecksum},
                                                      'tcCredCrc':{CredentialsChecksum},'station':'2.0.5(corecell/std)',
                                                      'model':'corecell','package':null,'keys':[]}}");
            var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(cupsRequest);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}
