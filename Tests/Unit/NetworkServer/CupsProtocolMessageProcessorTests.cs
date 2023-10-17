// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.IO;
    using System.IO.Pipelines;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
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
        private readonly Mock<ILogger<CupsProtocolMessageProcessor>> logger;
        private readonly CupsProtocolMessageProcessor processor;

        private const string StationEuiString = "aaaa:bbff:fecc:dddd";
        private const string CupsUri = "https://localhost:5002";
        private const string TcUri = "wss://localhost:5001";
        private const string FwUrl = "https://storage.blob.core.windows.net/fwupgrades/station-version?queryString=a";
        private const uint CredentialsChecksum = 12345;
        private const string Package = "1.0.0";
        private const uint KeyChecksum = 12345;

        public CupsProtocolMessageProcessorTests()
        {
            this.basicsStationConfigurationService = new Mock<IBasicsStationConfigurationService>();
            this.deviceAPIServiceBase = new Mock<LoRaDeviceAPIServiceBase>();
            this.logger = new Mock<ILogger<CupsProtocolMessageProcessor>>();
            this.processor = new CupsProtocolMessageProcessor(this.basicsStationConfigurationService.Object,
                                                              this.deviceAPIServiceBase.Object,
                                                              this.logger.Object,
                                                              // Do not pass meter since metric testing will be unreliable due to interference from test classes running in parallel.
                                                              null);
        }

        [Fact]
        public async Task HandleUpdateInfoAsync_Succeeds()
        {
            // setup valid http context
            using var memoryStream = new MemoryStream();
            var (httpContext, _, _) = SetupHttpContextWithRequest(CupsRequestJson, memoryStream);

            // setting up the twin in such a way that there are no updates
            var cupsTwinInfo = new CupsTwinInfo(new Uri(CupsUri),
                                                new Uri(TcUri),
                                                CredentialsChecksum,
                                                CredentialsChecksum,
                                                string.Empty,
                                                string.Empty,
                                                Package,
                                                KeyChecksum,
                                                string.Empty,
                                                new Uri(FwUrl));
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
        public async Task HandleUpdateInfoAsync_Fails_WithInvalidContentLength(int? requestContentLength)
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
        [InlineData(/*lang=json*/ "{'router':'invalidEui','cupsUri':'https://cups:5002', 'tcUri':'wss://lns:5001', 'cupsCredCrc':1, 'tcCredCrc':1,'station':'2.0.5','model':'m','package':null,'keys':[]}", typeof(FormatException))]
        [InlineData(/*lang=json*/ "{'router':'aabb:ccff:fe00:1122','cupsUri':'https:/cups:5002', 'tcUri':'wss://lns:5001', 'cupsCredCrc':1, 'tcCredCrc':1,'station':'2.0.5','model':'m','package':null,'keys':[]}", typeof(UriFormatException))]
        [InlineData(/*lang=json*/ "{'router':'aabb:ccff:fe00:1122','cupsUri':'https://cups:5002', 'tcUri':'wss:/lns:5001', 'cupsCredCrc':1, 'tcCredCrc':1,'station':'2.0.5','model':'m','package':null,'keys':[]}", typeof(UriFormatException))]
        [InlineData(/*lang=json*/ "{'router':'aabb:ccff:fe00:1122','cupsUri':'https://cups:5002', 'tcUri':'wss://lns:5001', 'cupsCredCrc':null, 'tcCredCrc':1,'station':'2.0.5','model':'m','package':null,'keys':[]}", typeof(JsonException))]
        [InlineData(/*lang=json*/ "{'router':'aabb:ccff:fe00:1122','cupsUri':'https://cups:5002', 'tcUri':'wss://lns:5001', 'cupsCredCrc':1, 'tcCredCrc':null,'station':'2.0.5','model':'m','package':null,'keys':[]}", typeof(JsonException))]
        [InlineData(/*lang=json*/ "{'router':'aabb:ccff:fe00:1122','cupsUri':'https://cups:5002', 'tcUri':'wss://lns:5001', 'cupsCredCrc':1, 'tcCredCrc':1,'station':'2.0.5','model':'m','package':123,'keys':[]}", typeof(JsonException))]
        [InlineData(/*lang=json*/ "{'router':'aabb:ccff:fe00:1122','cupsUri':'https://cups:5002', 'tcUri':'wss://lns:5001', 'cupsCredCrc':1, 'tcCredCrc':1,'station':'2.0.5','model':'m','package':'1.0.0','keys':['a']}", typeof(JsonException))]
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
            Assert.Contains(this.logger.GetLogInvocations(), args => args.Exception is { } exception && exception.GetType() == exceptionType);
        }

        [Fact]
        public async Task HandleUpdateInfoAsync_Invokes_FetchFirmware_WhenUpdateAvailable()
        {
            // setup
            using var memoryStream = new MemoryStream();
            var strictifiedInput = JsonUtil.Strictify(CupsRequestJson);
            var (httpContext, httpRequest, _) = SetupHttpContextWithRequest(strictifiedInput, memoryStream);
            _ = httpRequest.Setup(r => r.ContentLength).Returns(Encoding.UTF8.GetByteCount(strictifiedInput));

            var signatureBase64 = "ABCD";
            // setting up the twin in such a way that there is a fw update but no matching checksum
            var cupsTwinInfo = new CupsTwinInfo(new Uri(CupsUri),
                                                new Uri(TcUri),
                                                CredentialsChecksum,
                                                CredentialsChecksum,
                                                string.Empty,
                                                string.Empty,
                                                "anotherVersion",
                                                KeyChecksum,
                                                signatureBase64,
                                                new Uri(FwUrl));
            _ = this.basicsStationConfigurationService.Setup(m => m.GetCupsConfigAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                                      .Returns(Task.FromResult(cupsTwinInfo));

            var firmwareBytes = new byte[] { 1, 2, 3 };
            using var httpContent = new ByteArrayContent(firmwareBytes);
            _ = this.deviceAPIServiceBase.Setup(m => m.FetchStationFirmwareAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                         .ReturnsAsync(httpContent);
            // act
            await this.processor.HandleUpdateInfoAsync(httpContext.Object, default);

            // assert

            this.deviceAPIServiceBase.Verify(m => m.FetchStationFirmwareAsync(StationEui.Parse(StationEuiString), It.IsAny<CancellationToken>()), Times.Once);

            var expectedHeader = new CupsUpdateInfoResponseHeader
            {
                UpdateSignature = Convert.FromBase64String(signatureBase64),
                SignatureKeyCrc = KeyChecksum,
                UpdateDataLength = (uint)firmwareBytes.Length,
            };
            var expectedHeaderBytes = expectedHeader.Serialize(new byte[256].AsMemory()).ToArray();

            var response = memoryStream.ToArray();
            Assert.Equal(expectedHeaderBytes, response[..expectedHeaderBytes.Length]);
            Assert.Equal(firmwareBytes, response[expectedHeaderBytes.Length..]);
        }

        [Fact]
        public async Task HandleUpdateInfoAsync_Fails_WithNoMatchingKeyChecksum()
        {
            // setup
            var strictifiedInput = JsonUtil.Strictify(CupsRequestJson);
            var (httpContext, httpRequest, _) = SetupHttpContextWithRequest(strictifiedInput, null);
            _ = httpRequest.Setup(r => r.ContentLength).Returns(Encoding.UTF8.GetByteCount(strictifiedInput));

            // setting up the twin in such a way that there is a fw update but no matching checksum
            var cupsTwinInfo = new CupsTwinInfo(new Uri(CupsUri),
                                                new Uri(TcUri),
                                                CredentialsChecksum,
                                                CredentialsChecksum,
                                                string.Empty,
                                                string.Empty,
                                                "anotherVersion",
                                                6789,
                                                string.Empty,
                                                new Uri(FwUrl));
            _ = this.basicsStationConfigurationService.Setup(m => m.GetCupsConfigAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                                      .Returns(Task.FromResult(cupsTwinInfo));
            // act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await this.processor.HandleUpdateInfoAsync(httpContext.Object, default));

            Assert.Contains("checksum is not available", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task HandleUpdateInfoAsync_Fails_WithZeroFirmwareLength()
        {
            // setup
            var strictifiedInput = JsonUtil.Strictify(CupsRequestJson);
            var (httpContext, httpRequest, _) = SetupHttpContextWithRequest(strictifiedInput, null);
            _ = httpRequest.Setup(r => r.ContentLength).Returns(Encoding.UTF8.GetByteCount(strictifiedInput));

            // setting up the twin in such a way that there is a fw update but no matching checksum
            var cupsTwinInfo = new CupsTwinInfo(new Uri(CupsUri),
                                                new Uri(TcUri),
                                                CredentialsChecksum,
                                                CredentialsChecksum,
                                                string.Empty,
                                                string.Empty,
                                                "anotherVersion",
                                                KeyChecksum,
                                                string.Empty,
                                                new Uri(FwUrl));

            using var httpContent = new StringContent("firmware");
            httpContent.Headers.ContentLength = 0;

            _ = this.deviceAPIServiceBase.Setup(m => m.FetchStationFirmwareAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                         .ReturnsAsync(httpContent);
            _ = this.basicsStationConfigurationService.Setup(m => m.GetCupsConfigAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                                      .Returns(Task.FromResult(cupsTwinInfo));
            // act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await this.processor.HandleUpdateInfoAsync(httpContext.Object, default));
            Assert.Contains("Firmware could not be properly downloaded from function", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task HandleUpdateInfoAsync_Fails_WithNotValidFirmwareLength()
        {
            // setup
            var strictifiedInput = JsonUtil.Strictify(CupsRequestJson);
            var (httpContext, httpRequest, _) = SetupHttpContextWithRequest(strictifiedInput, null);
            _ = httpRequest.Setup(r => r.ContentLength).Returns(Encoding.UTF8.GetByteCount(strictifiedInput));

            // setting up the twin in such a way that there is a fw update but no matching checksum
            var cupsTwinInfo = new CupsTwinInfo(new Uri(CupsUri),
                                                new Uri(TcUri),
                                                CredentialsChecksum,
                                                CredentialsChecksum,
                                                string.Empty,
                                                string.Empty,
                                                "anotherVersion",
                                                KeyChecksum,
                                                string.Empty,
                                                new Uri(FwUrl));

            using var httpContent = new StringContent("firmware");
            httpContent.Headers.ContentLength = long.MaxValue;

            _ = this.deviceAPIServiceBase.Setup(m => m.FetchStationFirmwareAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                         .ReturnsAsync(httpContent);
            _ = this.basicsStationConfigurationService.Setup(m => m.GetCupsConfigAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                                      .Returns(Task.FromResult(cupsTwinInfo));
            // act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await this.processor.HandleUpdateInfoAsync(httpContext.Object, default));
            Assert.Contains("Firmware size can't be greater", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task HandleUpdateInfoAsync_Should_Conditionally_Invoke_FetchCredentialsAsync(bool cupsMismatch, bool tcMismatch)
        {
            // setup
            using var memoryStream = new MemoryStream();
            var strictifiedInput = JsonUtil.Strictify(CupsRequestJson);
            var (httpContext, httpRequest, _) = SetupHttpContextWithRequest(strictifiedInput, memoryStream);
            _ = httpRequest.Setup(r => r.ContentLength).Returns(Encoding.UTF8.GetByteCount(strictifiedInput));

            // setting up the twin in such a way that there is a fw update but no matching checksum
            var cupsTwinInfo = new CupsTwinInfo(new Uri(CupsUri),
                                                new Uri(TcUri),
                                                cupsMismatch ? 0U : CredentialsChecksum,
                                                tcMismatch ? 0U : CredentialsChecksum,
                                                string.Empty,
                                                string.Empty,
                                                Package,
                                                KeyChecksum,
                                                string.Empty,
                                                new Uri(FwUrl));

            _ = this.basicsStationConfigurationService.Setup(m => m.GetCupsConfigAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                                      .Returns(Task.FromResult(cupsTwinInfo));

            var credentialBase64 = "ABCD";
            _ = this.deviceAPIServiceBase.Setup(m => m.FetchStationCredentialsAsync(It.IsAny<StationEui>(), It.IsAny<ConcentratorCredentialType>(), It.IsAny<CancellationToken>()))
                                         .ReturnsAsync(credentialBase64);

            // act
            await this.processor.HandleUpdateInfoAsync(httpContext.Object, default);

            // assert
            if (cupsMismatch)
            {
                this.deviceAPIServiceBase.Verify(m => m.FetchStationCredentialsAsync(StationEui.Parse(StationEuiString), ConcentratorCredentialType.Cups, It.IsAny<CancellationToken>()), Times.Once);
            }
            if (tcMismatch)
            {
                this.deviceAPIServiceBase.Verify(m => m.FetchStationCredentialsAsync(StationEui.Parse(StationEuiString), ConcentratorCredentialType.Lns, It.IsAny<CancellationToken>()), Times.Once);
            }
            if (!cupsMismatch && !tcMismatch)
            {
                this.deviceAPIServiceBase.Verify(m => m.FetchStationCredentialsAsync(StationEui.Parse(StationEuiString), It.IsAny<ConcentratorCredentialType>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        private static readonly string CupsRequestJson = JsonSerializer.Serialize(new
        {
            router = StationEuiString,
            cupsUri = CupsUri,
            tcUri = TcUri,
            cupsCredCrc = CredentialsChecksum,
            tcCredCrc = CredentialsChecksum,
            station = "2.0.5(corecell/std)",
            model = "corecell",
            package = Package,
            keys = new[] { KeyChecksum }
        });

        private static (Mock<HttpContext>, Mock<HttpRequest>, Mock<HttpResponse>)
            SetupHttpContextWithRequest(string request, Stream receivedResponse)
        {
            var httpContext = new Mock<HttpContext>();
            var httpRequest = new Mock<HttpRequest>();
            var requestStream = new MemoryStream(Encoding.UTF8.GetBytes(request));
            _ = httpRequest.Setup(r => r.ContentLength).Returns(requestStream.Length);
            _ = httpRequest.Setup(r => r.Body).Returns(requestStream);
            _ = httpRequest.Setup(r => r.BodyReader).Returns(PipeReader.Create(requestStream));
            _ = httpContext.Setup(m => m.Request).Returns(httpRequest.Object);
            var httpResponse = new Mock<HttpResponse>();
            var bodyWriter = receivedResponse is null ? Mock.Of<PipeWriter>() : PipeWriter.Create(receivedResponse);
            _ = httpResponse.SetupProperty(m => m.StatusCode);
            _ = httpResponse.SetupProperty(m => m.ContentType);
            _ = httpResponse.SetupProperty(m => m.ContentLength);
            _ = httpResponse.Setup(m => m.BodyWriter).Returns(bodyWriter);
            _ = httpContext.Setup(m => m.Response).Returns(httpResponse.Object);
            return (httpContext, httpRequest, httpResponse);
        }
    }
}
