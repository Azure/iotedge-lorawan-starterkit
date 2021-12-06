// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.IO.Pipelines;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
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
        private readonly Mock<ILogger<CupsProtocolMessageProcessor>> logger;
        private readonly CupsProtocolMessageProcessor processor;

        private const string StationEui = "aaaa:bbff:fecc:dddd";
        private const string CupsUri = "https://localhost:5002";
        private const string TcUri = "wss://localhost:5001";
        private const uint CredentialsChecksum = 12345;

        public CupsProtocolMessageProcessorTests()
        {
            this.basicsStationConfigurationService = new Mock<IBasicsStationConfigurationService>();
            this.deviceAPIServiceBase = new Mock<LoRaDeviceAPIServiceBase>();
            this.logger = new Mock<ILogger<CupsProtocolMessageProcessor>>();
            this.processor = new CupsProtocolMessageProcessor(this.basicsStationConfigurationService.Object,
                                                              this.deviceAPIServiceBase.Object,
                                                              this.logger.Object);
        }

        [Fact]
        public async Task HandleUpdateInfoAsync_Succeeds()
        {
            // setup valid http context
            using var receivedResponse = MemoryPool<byte>.Shared.Rent();
            var (httpContext, _, _) = SetupHttpContextWithRequest(CupsRequestJson, receivedResponse.Memory);

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
            Assert.Equal((int)HttpStatusCode.OK, httpContext.Object.Response.StatusCode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(1)]
        [InlineData(int.MaxValue)]
        public async Task HandleUpdateInfoAsync_Fails_WithInvalidContentLength(long? requestContentLength)
        {
            // setup
            var (httpContext, httpRequest, _) = SetupHttpContextWithRequest(CupsRequestJson, null);
            _ = httpRequest.Setup(r => r.ContentLength).Returns(requestContentLength);

            // act
            await this.processor.HandleUpdateInfoAsync(httpContext.Object, default);

            // assert
            Assert.Equal((int)HttpStatusCode.BadRequest, httpContext.Object.Response.StatusCode);
        }

        [Theory]
        [InlineData("{'router':'invalidEui','cupsUri':'https://cups:5002', 'tcUri':'wss://lns:5001', 'cupsCredCrc':1, 'tcCredCrc':1,'station':'2.0.5','model':'m','package':null,'keys':[]}", typeof(FormatException))]
        [InlineData("{'router':'aabb:ccff:fe00:1122','cupsUri':'https:/cups:5002', 'tcUri':'wss://lns:5001', 'cupsCredCrc':1, 'tcCredCrc':1,'station':'2.0.5','model':'m','package':null,'keys':[]}", typeof(UriFormatException))]
        [InlineData("{'router':'aabb:ccff:fe00:1122','cupsUri':'https://cups:5002', 'tcUri':'wss:/lns:5001', 'cupsCredCrc':1, 'tcCredCrc':1,'station':'2.0.5','model':'m','package':null,'keys':[]}", typeof(UriFormatException))]
        [InlineData("{'router':'aabb:ccff:fe00:1122','cupsUri':'https://cups:5002', 'tcUri':'wss://lns:5001', 'cupsCredCrc':null, 'tcCredCrc':1,'station':'2.0.5','model':'m','package':null,'keys':[]}", typeof(JsonException))]
        [InlineData("{'router':'aabb:ccff:fe00:1122','cupsUri':'https://cups:5002', 'tcUri':'wss://lns:5001', 'cupsCredCrc':1, 'tcCredCrc':null,'station':'2.0.5','model':'m','package':null,'keys':[]}", typeof(JsonException))]
        public async Task HandleUpdateInfoAsync_Fails_WithInvalidInput(string input, Type exceptionType)
        {
            // setup
            var strictifiedInput = JsonUtil.Strictify(input);
            var (httpContext, httpRequest, _) = SetupHttpContextWithRequest(strictifiedInput, null);
            _ = httpRequest.Setup(r => r.ContentLength).Returns(Encoding.UTF8.GetByteCount(strictifiedInput));

            // act
            await this.processor.HandleUpdateInfoAsync(httpContext.Object, default);

            // assert
            Assert.Equal((int)HttpStatusCode.BadRequest, httpContext.Object.Response.StatusCode);
            Assert.Contains(this.logger.Invocations, i => i.Arguments.Any(a => a.GetType() == exceptionType));
        }

        private static readonly string CupsRequestJson = JsonSerializer.Serialize(new
        {
            router = StationEui,
            cupsUri = CupsUri,
            tcUri = TcUri,
            cupsCredCrc = CredentialsChecksum,
            tcCredCrc = CredentialsChecksum,
            station = "2.0.5(corecell/std)",
            model = "corecell",
            package = (string)null,
            keys = Array.Empty<int>()
        });

        private static (Mock<HttpContext>, Mock<HttpRequest>, Mock<HttpResponse>)
            SetupHttpContextWithRequest(string request, Memory<byte> receivedResponse)
        {
            var httpContext = new Mock<HttpContext>();
            var httpRequest = new Mock<HttpRequest>();
            var requestStream = new MemoryStream(Encoding.UTF8.GetBytes(request));
            _ = httpRequest.Setup(r => r.ContentLength).Returns(requestStream.Length);
            _ = httpRequest.Setup(r => r.Body).Returns(requestStream);
            _ = httpRequest.Setup(r => r.BodyReader).Returns(PipeReader.Create(requestStream));
            _ = httpContext.Setup(m => m.Request).Returns(httpRequest.Object);
            var httpResponse = new Mock<HttpResponse>();
            var bodyWriter = new Mock<PipeWriter>();
            _ = bodyWriter.Setup(m => m.WriteAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
                      .Callback<ReadOnlyMemory<byte>, CancellationToken>((memoryPortion, _) =>
                      {
                          memoryPortion.CopyTo(receivedResponse);
                      });
            _ = httpResponse.SetupProperty(m => m.StatusCode);
            _ = httpResponse.SetupProperty(m => m.ContentType);
            _ = httpResponse.SetupProperty(m => m.ContentLength);
            _ = httpResponse.Setup(m => m.BodyWriter).Returns(bodyWriter.Object);
            _ = httpContext.Setup(m => m.Response).Returns(httpResponse.Object);
            return (httpContext, httpRequest, httpResponse);
        }
    }
}
