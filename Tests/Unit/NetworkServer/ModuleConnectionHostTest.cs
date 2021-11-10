// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using Bogus;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation.ModuleConnection;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using System;
    using System.Configuration;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class ModuleConnectionHostTest
    {

        private readonly Mock<ILoRaModuleClientFactory> loRaModuleClientFactory = new();
        private readonly Mock<ILoraModuleClient> loRaModuleClient = new();
        private readonly LoRaDeviceAPIServiceBase loRaDeviceApiServiceBase = Mock.Of<LoRaDeviceAPIServiceBase>();
        private readonly Faker faker = new Faker();

        public ModuleConnectionHostTest()
        {
            this.loRaModuleClient.Setup(x => x.DisposeAsync());
            this.loRaModuleClientFactory.Setup(x => x.CreateAsync()).ReturnsAsync(loRaModuleClient.Object);
        }

        [Fact]
        public void When_Constructor_Receives_Null_Parameters_Should_Throw()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = Mock.Of<IClassCDeviceMessageSender>();
            var loRaDeviceRegistry = Mock.Of<ILoRaDeviceRegistry>();
            var loRaModuleClientFactory = Mock.Of<ILoRaModuleClientFactory>();

            // ASSERT
            ArgumentNullException ex;
            ex = Assert.Throws<ArgumentNullException>(() => new ModuleConnectionHost(null, classCMessageSender, loRaModuleClientFactory, loRaDeviceRegistry, loRaDeviceApiServiceBase));
            Assert.Equal("networkServerConfiguration", ex.ParamName);
            ex = Assert.Throws<ArgumentNullException>(() => new ModuleConnectionHost(networkServerConfiguration, null, loRaModuleClientFactory, loRaDeviceRegistry, loRaDeviceApiServiceBase));
            Assert.Equal("defaultClassCDevicesMessageSender", ex.ParamName);
            ex = Assert.Throws<ArgumentNullException>(() => new ModuleConnectionHost(networkServerConfiguration, classCMessageSender, null, loRaDeviceRegistry, loRaDeviceApiServiceBase));
            Assert.Equal("loRaModuleClientFactory", ex.ParamName);
            ex = Assert.Throws<ArgumentNullException>(() => new ModuleConnectionHost(networkServerConfiguration, classCMessageSender, loRaModuleClientFactory, null, loRaDeviceApiServiceBase));
            Assert.Equal("loRaDeviceRegistry", ex.ParamName);
            ex = Assert.Throws<ArgumentNullException>(() => new ModuleConnectionHost(networkServerConfiguration, classCMessageSender, loRaModuleClientFactory, loRaDeviceRegistry, null));
            Assert.Equal("loRaDeviceAPIService", ex.ParamName);
        }

        [Fact]
        public async Task On_Desired_Properties_Correct_Update_Should_Update()
        {
            var networkServerConfiguration = Mock.Of<NetworkServerConfiguration>();
            var classCMessageSender = Mock.Of<IClassCDeviceMessageSender>();
            var loRaDeviceRegistry = Mock.Of<ILoRaDeviceRegistry>();
            var loRaModuleClientFactory = Mock.Of<ILoRaModuleClientFactory>();

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender, loRaModuleClientFactory, loRaDeviceRegistry, loRaDeviceApiServiceBase);
            var url1 = this.faker.Internet.Url();
            var authCode = this.faker.Internet.Password();

            var input = JsonSerializer.Serialize(new
            {
                FacadeServerUrl = url1,
                FacadeAuthCode = authCode,
            });

            await moduleClient.OnDesiredPropertiesUpdate(new TwinCollection(input), null);
            Assert.Equal(url1 + "/", loRaDeviceApiServiceBase.URL.ToString());
            var url2 = this.faker.Internet.Url();
            var input2 = JsonSerializer.Serialize(new
            {
                FacadeServerUrl = url2,
                FacadeAuthCode = authCode,
            });
            await moduleClient.OnDesiredPropertiesUpdate(new TwinCollection(input2), null);
            Assert.Equal(url2 + "/", loRaDeviceApiServiceBase.URL.ToString());
            Assert.Equal(authCode, loRaDeviceApiServiceBase.AuthCode.ToString());

        }

        [Theory]
        [InlineData("{ FacadeServerUrl: 'url2', FacadeAuthCode: 'authCode' }")]// not a url
        [InlineData("{ FacadeAuthCode: 'authCode' }")] // no Url
        [InlineData("{ FacadeServerUrl: '', FacadeAuthCode: 'authCode' }")]// empty url
        public async Task On_Desired_Properties_Incorrect_Update_Should_Not_Update(string twinUpdate)
        {
            var facadeUri = faker.Internet.Url();
            var facadeCode = faker.Internet.Password();
            var networkServerConfiguration = new NetworkServerConfiguration
            {
                FacadeServerUrl = new Uri(facadeUri),
                FacadeAuthCode = facadeCode
            };

            var localLoRaDeviceApiServiceBase = new LoRaDeviceAPIService(networkServerConfiguration, Mock.Of<IServiceFacadeHttpClientProvider>());
            var classCMessageSender = Mock.Of<IClassCDeviceMessageSender>();
            var loRaDeviceRegistry = Mock.Of<ILoRaDeviceRegistry>();
            var loRaModuleClientFactory = Mock.Of<ILoRaModuleClientFactory>();

            await using var moduleClientFactory = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender, loRaModuleClientFactory, loRaDeviceRegistry, localLoRaDeviceApiServiceBase);

            await moduleClientFactory.OnDesiredPropertiesUpdate(new TwinCollection(twinUpdate), null);
            Assert.Equal(facadeUri + "/", localLoRaDeviceApiServiceBase.URL.ToString());
            Assert.Equal(facadeCode, localLoRaDeviceApiServiceBase.AuthCode);
        }

        [Fact]
        public async Task InitModuleAsync_Update_Should_Perform_Happy_Path()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            var loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);

            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;
            var facadeUri = this.faker.Internet.Url();
            var facadeCode = this.faker.Internet.Password();
            var twinProperty = new TwinProperties
            {
                Desired = new TwinCollection(
                    JsonSerializer.Serialize(new
                    {
                        FacadeServerUrl = facadeUri,
                        FacadeAuthCode = facadeCode,
                    }))
            };
            
            loRaModuleClient.Setup(x => x.GetTwinAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Twin(twinProperty));

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, this.loRaModuleClientFactory.Object, loRaDeviceRegistry.Object, loRaDeviceApiServiceBase);
            await moduleClient.CreateAsync(CancellationToken.None);
            Assert.Equal(facadeUri + "/", loRaDeviceApiServiceBase.URL.ToString());
            Assert.Equal(facadeCode, loRaDeviceApiServiceBase.AuthCode);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("{ FacadeAuthCode: 'asdasdada' }")]
        public async Task InitModuleAsync_Fails_When_Required_Twins_Are_Not_Set(string twin)
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            var loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            var loRaModuleClient = new Mock<ILoraModuleClient>();
            loRaModuleClient.Setup(x => x.DisposeAsync());
            var loRaModuleClientFactory = new Mock<ILoRaModuleClientFactory>();

            loRaModuleClientFactory.Setup(x => x.CreateAsync()).ReturnsAsync(loRaModuleClient.Object);

            var twinProperty = new TwinProperties
            {
                Desired = new TwinCollection(twin)
            };
            loRaModuleClient.Setup(x => x.GetTwinAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Twin(twinProperty));

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, this.loRaModuleClientFactory.Object, loRaDeviceRegistry.Object, loRaDeviceApiServiceBase);
            await Assert.ThrowsAsync<ConfigurationErrorsException>(() => moduleClient.CreateAsync(CancellationToken.None));
        }

        [Fact]
        public async Task InitModuleAsync_Fails_When_Fail_IoT_Hub_Communication()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            var loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);

            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;

            loRaModuleClient.Setup(x => x.GetTwinAsync(It.IsAny<CancellationToken>())).Throws<IotHubCommunicationException>();

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, this.loRaModuleClientFactory.Object, loRaDeviceRegistry.Object, loRaDeviceApiServiceBase);
            var ex = await Assert.ThrowsAsync<LoRaProcessingException>(() => moduleClient.CreateAsync(CancellationToken.None));
            Assert.Equal(LoRaProcessingErrorCode.TwinFetchFailed, ex.ErrorCode);
        }

        [Fact]
        public async Task OnDirectMethodCall_ClearCache_When_Correct_Should_Work()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            var loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            loRaDeviceRegistry.Setup(x => x.ResetDeviceCache());

            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, this.loRaModuleClientFactory.Object, loRaDeviceRegistry.Object, loRaDeviceApiServiceBase);
            await moduleClient.OnDirectMethodCalled(new Microsoft.Azure.Devices.Client.MethodRequest(Constants.CloudToDeviceClearCache), null);
            loRaDeviceRegistry.VerifyAll();
        }

        [Fact]
        public async Task OnDirectMethodCall_CloudToDeviceDecoderElementName_When_Correct_Should_Work()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            classCMessageSender.Setup(x => x.SendAsync(It.IsAny<ReceivedLoRaCloudToDeviceMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);

            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, this.loRaModuleClientFactory.Object, loRaDeviceRegistry.Object, loRaDeviceApiServiceBase);

            var c2d = "{\"test\":\"asd\"}";

            var response = await moduleClient.OnDirectMethodCalled(new Microsoft.Azure.Devices.Client.MethodRequest(Constants.CloudToDeviceDecoderElementName, Encoding.UTF8.GetBytes(c2d), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)), null);
            Assert.Equal((int)HttpStatusCode.OK, response.Status);
        }

        [Fact]
        public async Task OnDirectMethodCall_When_Null_Or_Empty_MethodName_Should_Throw()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            classCMessageSender.Setup(x => x.SendAsync(It.IsAny<ReceivedLoRaCloudToDeviceMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);

            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, this.loRaModuleClientFactory.Object, loRaDeviceRegistry.Object, loRaDeviceApiServiceBase);

            await Assert.ThrowsAnyAsync<ArgumentNullException>(async () => await moduleClient.OnDirectMethodCalled(null, null));
        }

        [Fact]
        public async Task OnDirectMethodCall_CloudToDeviceDecoderElementName_When_Incorrect_Should_Return_NotFound()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            classCMessageSender.Setup(x => x.SendAsync(It.IsAny<ReceivedLoRaCloudToDeviceMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);

            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, this.loRaModuleClientFactory.Object, loRaDeviceRegistry.Object, loRaDeviceApiServiceBase);
            var c2d = "{\"test\":\"asd\"}";

            var response = await moduleClient.OnDirectMethodCalled(new Microsoft.Azure.Devices.Client.MethodRequest(this.faker.Random.String2(8), Encoding.UTF8.GetBytes(c2d)), null);
            Assert.Equal((int)HttpStatusCode.BadRequest, response.Status);
        }

        [Fact]
        public async Task SendCloudToDeviceMessageAsync_When_ClassC_Msg_Is_Null_Or_Empty_Should_Return_Not_Found()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            classCMessageSender.Setup(x => x.SendAsync(It.IsAny<ReceivedLoRaCloudToDeviceMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            var loRaModuleClient = new Mock<ILoraModuleClient>();
            loRaModuleClient.Setup(x => x.DisposeAsync());
            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;
            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, this.loRaModuleClientFactory.Object, loRaDeviceRegistry.Object, loRaDeviceApiServiceBase);

            var response = await moduleClient.OnDirectMethodCalled(new Microsoft.Azure.Devices.Client.MethodRequest(Constants.CloudToDeviceDecoderElementName, null), null);
            Assert.Equal((int)HttpStatusCode.BadRequest, response.Status);

            var response2 = await moduleClient.OnDirectMethodCalled(new Microsoft.Azure.Devices.Client.MethodRequest(Constants.CloudToDeviceDecoderElementName, Array.Empty<byte>()), null);
            Assert.Equal((int)HttpStatusCode.BadRequest, response2.Status);
        }

        [Fact]
        public async Task SendCloudToDeviceMessageAsync_When_ClassC_Msg_Is_Not_CorrectJson_Should_Return_Not_Found()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            classCMessageSender.Setup(x => x.SendAsync(It.IsAny<ReceivedLoRaCloudToDeviceMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);

            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;
            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, this.loRaModuleClientFactory.Object, loRaDeviceRegistry.Object, loRaDeviceApiServiceBase);

            var response = await moduleClient.OnDirectMethodCalled(new Microsoft.Azure.Devices.Client.MethodRequest(Constants.CloudToDeviceDecoderElementName, Encoding.UTF8.GetBytes(faker.Random.String2(10))), null);
            Assert.Equal((int)HttpStatusCode.BadRequest, response.Status);
        }
    }
}
