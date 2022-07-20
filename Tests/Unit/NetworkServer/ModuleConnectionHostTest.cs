// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using Bogus;
    using global::LoRaTools;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation.ModuleConnection;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using System;
    using System.Configuration;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public sealed class ModuleConnectionHostTest : IAsyncDisposable
    {
        private readonly NetworkServerConfiguration networkServerConfiguration;
        private readonly Mock<ILoRaModuleClientFactory> loRaModuleClientFactory = new();
        private readonly Mock<ILoraModuleClient> loRaModuleClient = new();
        private readonly LoRaDeviceAPIServiceBase loRaDeviceApiServiceBase = Mock.Of<LoRaDeviceAPIServiceBase>();
        private readonly Faker faker = new Faker();
        private readonly Mock<ILnsRemoteCallHandler> lnsRemoteCall;
        private readonly ModuleConnectionHost subject;

        public ModuleConnectionHostTest()
        {
            this.networkServerConfiguration = new NetworkServerConfiguration();
            this.loRaModuleClient.Setup(x => x.DisposeAsync());
            this.loRaModuleClientFactory.Setup(x => x.CreateAsync()).ReturnsAsync(loRaModuleClient.Object);
            this.lnsRemoteCall = new Mock<ILnsRemoteCallHandler>();
            this.subject = new ModuleConnectionHost(this.networkServerConfiguration,
                                                    this.loRaModuleClientFactory.Object,
                                                    this.loRaDeviceApiServiceBase,
                                                    this.lnsRemoteCall.Object,
                                                    NullLogger<ModuleConnectionHost>.Instance,
                                                    TestMeter.Instance);
        }

        [Fact]
        public void When_Constructor_Receives_Null_Parameters_Should_Throw()
        {
            // ASSERT
            ArgumentNullException ex;
            ex = Assert.Throws<ArgumentNullException>(() => new ModuleConnectionHost(null, this.loRaModuleClientFactory.Object, this.loRaDeviceApiServiceBase, this.lnsRemoteCall.Object, NullLogger<ModuleConnectionHost>.Instance, TestMeter.Instance));
            Assert.Equal("networkServerConfiguration", ex.ParamName);
            ex = Assert.Throws<ArgumentNullException>(() => new ModuleConnectionHost(networkServerConfiguration, null, this.loRaDeviceApiServiceBase, this.lnsRemoteCall.Object, NullLogger<ModuleConnectionHost>.Instance, TestMeter.Instance));
            Assert.Equal("loRaModuleClientFactory", ex.ParamName);
            ex = Assert.Throws<ArgumentNullException>(() => new ModuleConnectionHost(networkServerConfiguration, this.loRaModuleClientFactory.Object, null, this.lnsRemoteCall.Object, NullLogger<ModuleConnectionHost>.Instance, TestMeter.Instance));
            Assert.Equal("loRaDeviceAPIService", ex.ParamName);
            ex = Assert.Throws<ArgumentNullException>(() => new ModuleConnectionHost(networkServerConfiguration, this.loRaModuleClientFactory.Object, this.loRaDeviceApiServiceBase, null, NullLogger<ModuleConnectionHost>.Instance, TestMeter.Instance));
            Assert.Equal("lnsRemoteCallHandler", ex.ParamName);
        }

        [Fact]
        public async Task On_Desired_Properties_Correct_Update_Should_Update_Api_Service_Configuration()
        {
            var url1 = this.faker.Internet.Url();
            var authCode = this.faker.Internet.Password();

            var input = JsonSerializer.Serialize(new
            {
                FacadeServerUrl = url1,
                FacadeAuthCode = authCode,
            });

            await this.subject.OnDesiredPropertiesUpdate(new TwinCollection(input), null);
            Assert.Equal(url1 + "/", loRaDeviceApiServiceBase.URL.ToString());
            var url2 = this.faker.Internet.Url();
            var input2 = JsonSerializer.Serialize(new
            {
                FacadeServerUrl = url2,
                FacadeAuthCode = authCode,
            });
            await this.subject.OnDesiredPropertiesUpdate(new TwinCollection(input2), null);
            Assert.Equal(url2 + "/", loRaDeviceApiServiceBase.URL.ToString());
            Assert.Equal(authCode, loRaDeviceApiServiceBase.AuthCode.ToString());
        }

        [Theory]
        [InlineData("{ FacadeServerUrl: 'url2', FacadeAuthCode: 'authCode' }")]// not a url
        [InlineData("{ FacadeAuthCode: 'authCode' }")] // no Url
        [InlineData("{ FacadeServerUrl: '', FacadeAuthCode: 'authCode' }")]// empty url
        public async Task On_Desired_Properties_Incorrect_Update_Should_Not_Update_Api_Service_Configuration(string twinUpdate)
        {
            var facadeUri = faker.Internet.Url();
            var facadeCode = faker.Internet.Password();
            var networkServerConfiguration = new NetworkServerConfiguration
            {
                FacadeServerUrl = new Uri(facadeUri),
                FacadeAuthCode = facadeCode
            };

            var localLoRaDeviceApiServiceBase = new LoRaDeviceAPIService(networkServerConfiguration, Mock.Of<IHttpClientFactory>(), NullLogger<LoRaDeviceAPIService>.Instance, TestMeter.Instance);
            await using var moduleClientFactory = new ModuleConnectionHost(networkServerConfiguration, this.loRaModuleClientFactory.Object, localLoRaDeviceApiServiceBase, this.lnsRemoteCall.Object, NullLogger<ModuleConnectionHost>.Instance, TestMeter.Instance);

            await moduleClientFactory.OnDesiredPropertiesUpdate(new TwinCollection(twinUpdate), null);
            Assert.Equal(facadeUri + "/", localLoRaDeviceApiServiceBase.URL.ToString());
            Assert.Equal(facadeCode, localLoRaDeviceApiServiceBase.AuthCode);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(400)]
        [InlineData(1000)]
        public async Task On_Desired_Properties_Correct_Update_Should_Update_Processing_Delay(int processingDelay)
        {
            Assert.Equal(LoRaWan.NetworkServer.Constants.DefaultProcessingDelayInMilliseconds, this.networkServerConfiguration.ProcessingDelayInMilliseconds);

            var input = JsonSerializer.Serialize(new
            {
                ProcessingDelayInMilliseconds = processingDelay,
            });

            await this.subject.OnDesiredPropertiesUpdate(new TwinCollection(input), null);
            Assert.Equal(processingDelay, networkServerConfiguration.ProcessingDelayInMilliseconds);
        }

        [Theory]
        [InlineData("{ ProcessingDelayInMilliseconds: -400 }")]
        [InlineData("{ ProcessingDelayInMilliseconds: '' }")]
        [InlineData("{ ProcessingDelay: 200 }")]
        public async Task On_Desired_Properties_Incorrect_Update_Should_Not_Update_Processing_Delay(string twinUpdate)
        {
            await this.subject.OnDesiredPropertiesUpdate(new TwinCollection(twinUpdate), null);
            Assert.Equal(LoRaWan.NetworkServer.Constants.DefaultProcessingDelayInMilliseconds, this.networkServerConfiguration.ProcessingDelayInMilliseconds);
        }

        [Fact]
        public async Task InitModuleAsync_Update_Should_Perform_Happy_Path()
        {
            var timeotNetworkServerConfiguration = new NetworkServerConfiguration()
            {
                // Change the iot edge timeout.
                IoTEdgeTimeout = 5
            };

            var facadeUri = this.faker.Internet.Url();
            var facadeCode = this.faker.Internet.Password();
            var processingDelay = 1000;
            var twinProperty = new TwinProperties
            {
                Desired = new TwinCollection(
                    JsonSerializer.Serialize(new
                    {
                        FacadeServerUrl = facadeUri,
                        FacadeAuthCode = facadeCode,
                        ProcessingDelayInMilliseconds = processingDelay
                    }))
            };
            
            loRaModuleClient.Setup(x => x.GetTwinAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Twin(twinProperty));

            await using var moduleClient = new ModuleConnectionHost(timeotNetworkServerConfiguration, this.loRaModuleClientFactory.Object, loRaDeviceApiServiceBase, this.lnsRemoteCall.Object, NullLogger<ModuleConnectionHost>.Instance, TestMeter.Instance);
            await moduleClient.CreateAsync(CancellationToken.None);
            Assert.Equal(facadeUri + "/", loRaDeviceApiServiceBase.URL.ToString());
            Assert.Equal(facadeCode, loRaDeviceApiServiceBase.AuthCode);
            Assert.Equal(processingDelay, timeotNetworkServerConfiguration.ProcessingDelayInMilliseconds);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("{ FacadeAuthCode: 'asdasdada' }")]
        public async Task InitModuleAsync_Fails_When_Required_Twins_Are_Not_Set(string twin)
        {
            var loRaModuleClient = new Mock<ILoraModuleClient>();
            loRaModuleClient.Setup(x => x.DisposeAsync());
            var loRaModuleClientFactory = new Mock<ILoRaModuleClientFactory>();

            loRaModuleClientFactory.Setup(x => x.CreateAsync()).ReturnsAsync(loRaModuleClient.Object);

            var twinProperty = new TwinProperties
            {
                Desired = new TwinCollection(twin)
            };
            loRaModuleClient.Setup(x => x.GetTwinAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Twin(twinProperty));

            await using var moduleClient = new ModuleConnectionHost(this.networkServerConfiguration, this.loRaModuleClientFactory.Object, loRaDeviceApiServiceBase, this.lnsRemoteCall.Object, NullLogger<ModuleConnectionHost>.Instance, TestMeter.Instance);
            await Assert.ThrowsAsync<ConfigurationErrorsException>(() => moduleClient.CreateAsync(CancellationToken.None));
        }

        [Theory]
        [InlineData("")]
        [InlineData("500 ms")]
        [InlineData("-200")]
        [InlineData("invalidDelay")]
        public async Task InitModuleAsync_Does_Not_Fail_When_Processing_Delay_Missing_Or_Incorrect(string processingDelay)
        {
            var facadeUri = this.faker.Internet.Url();
            var facadeCode = this.faker.Internet.Password();
            var twinProperty = new TwinProperties
            {
                Desired = new TwinCollection(
                    JsonSerializer.Serialize(new
                    {
                        FacadeServerUrl = facadeUri,
                        FacadeAuthCode = facadeCode,
                        ProcessingDelayInMilliseconds = processingDelay
                    }))
            };

            this.loRaModuleClient.Setup(x => x.GetTwinAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Twin(twinProperty));

            await this.subject.CreateAsync(CancellationToken.None);
            Assert.Equal(LoRaWan.NetworkServer.Constants.DefaultProcessingDelayInMilliseconds, this.networkServerConfiguration.ProcessingDelayInMilliseconds);
        }

        [Fact]
        public async Task InitModuleAsync_Fails_When_Fail_IoT_Hub_Communication()
        {
            // Change the iot edge timeout.
            this.networkServerConfiguration.IoTEdgeTimeout = 5;

            this.loRaModuleClient.Setup(x => x.GetTwinAsync(It.IsAny<CancellationToken>())).Throws<IotHubCommunicationException>();

            var ex = await Assert.ThrowsAsync<LoRaProcessingException>(() => this.subject.CreateAsync(CancellationToken.None));
            Assert.Equal(LoRaProcessingErrorCode.TwinFetchFailed, ex.ErrorCode);
        }

        [Fact]
        public async Task OnDirectMethodCall_Should_Invoke_ClearCache()
        {
            await this.subject.OnDirectMethodCalled(new MethodRequest(LoRaWan.NetworkServer.Constants.CloudToDeviceClearCache), null);
            this.lnsRemoteCall.Verify(l => l.ExecuteAsync(new LnsRemoteCall(RemoteCallKind.ClearCache, null), CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task OnDirectMethodCall_Should_Invoke_DropConnection()
        {
            // arrange
            var json = @"{""foo"":""bar""}";
            var methodRequest = new MethodRequest(LoRaWan.NetworkServer.Constants.CloudToDeviceCloseConnection, Encoding.UTF8.GetBytes(json));

            // act
            await this.subject.OnDirectMethodCalled(methodRequest, null);

            // assert
            this.lnsRemoteCall.Verify(l => l.ExecuteAsync(new LnsRemoteCall(RemoteCallKind.CloseConnection, json), CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task OnDirectMethodCall_Should_Invoke_SendCloudToDeviceMessageAsync()
        {
            // arrange
            var json = @"{""foo"":""bar""}";
            var methodRequest = new MethodRequest(LoRaWan.NetworkServer.Constants.CloudToDeviceDecoderElementName, Encoding.UTF8.GetBytes(json));

            // act
            await this.subject.OnDirectMethodCalled(methodRequest, null);

            // assert
            this.lnsRemoteCall.Verify(l => l.ExecuteAsync(new LnsRemoteCall(RemoteCallKind.CloudToDeviceMessage, json), CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task OnDirectMethodCall_When_Null_Or_Empty_MethodName_Should_Throw()
        {
            await Assert.ThrowsAnyAsync<ArgumentNullException>(async () => await this.subject.OnDirectMethodCalled(null, null));
        }

        public async ValueTask DisposeAsync() => await this.subject.DisposeAsync();
    }
}
