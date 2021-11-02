// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
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
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class ModuleConnectionHostTest
    {
        private readonly Faker faker = new Faker();

        [Fact]
        public void When_Constructor_Receives_Null_Parameters_Should_Throw()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = Mock.Of<IClassCDeviceMessageSender>();
            var loRaDeviceRegistry = Mock.Of<ILoRaDeviceRegistry>();

            // ASSERT
            Assert.Throws<ArgumentNullException>(() => new ModuleConnectionHost(null, classCMessageSender, loRaDeviceRegistry));
            Assert.Throws<ArgumentNullException>(() => new ModuleConnectionHost(networkServerConfiguration, null, loRaDeviceRegistry));
            Assert.Throws<ArgumentNullException>(() => new ModuleConnectionHost(networkServerConfiguration, classCMessageSender, null));
        }

        [Fact]
        public async Task On_Desired_Properties_Correct_Update_Should_Update()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = Mock.Of<IClassCDeviceMessageSender>();
            var loRaDeviceRegistry = Mock.Of<ILoRaDeviceRegistry>();

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender, loRaDeviceRegistry);
            var url1 = this.faker.Internet.Url();
            var input = $"{{\"FacadeServerUrl\": \"{url1}\"}}";
            await moduleClient.OnDesiredPropertiesUpdate(new Microsoft.Azure.Devices.Shared.TwinCollection(input), null);
            Assert.Equal(url1 + "/", networkServerConfiguration.FacadeServerUrl.ToString());
            var authCode = this.faker.Internet.Password();
            var url2 = this.faker.Internet.Url();
            var input2 = $"{{\"FacadeServerUrl\": \"{url2}\", \"FacadeAuthCode\":\"{authCode}\"}}";
            await moduleClient.OnDesiredPropertiesUpdate(new Microsoft.Azure.Devices.Shared.TwinCollection(input2), null);
            Assert.Equal(url2 + "/", networkServerConfiguration.FacadeServerUrl.ToString());
            Assert.Equal(authCode, networkServerConfiguration.FacadeAuthCode.ToString());

        }

        [Theory]
        [InlineData("{\"FacadeServerUrl\": \"url2\", \"FacadeAuthCode\":\"authCode\"}")]// not a url
        [InlineData("{\"FacadeAuthCode\":\"authCode\"}")] // no Url
        [InlineData("{\"FacadeServerUrl\": \"\", \"FacadeAuthCode\":\"authCode\"}")]// empty url

        public async Task On_Desired_Properties_Incorrect_Update_Should_Not_Update(string twinUpdate)
        {
            var facadeUri = faker.Internet.Url();
            var facadeCode = faker.Internet.Password();
            var networkServerConfiguration = new NetworkServerConfiguration
            {
                FacadeServerUrl = new Uri(facadeUri),
                FacadeAuthCode = facadeCode
            };
            var classCMessageSender = Mock.Of<IClassCDeviceMessageSender>();
            var loRaDeviceRegistry = Mock.Of<ILoRaDeviceRegistry>();

            await using var moduleClientFactory = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender, loRaDeviceRegistry);

            await moduleClientFactory.OnDesiredPropertiesUpdate(new Microsoft.Azure.Devices.Shared.TwinCollection(twinUpdate), null);
            Assert.Equal(facadeUri + "/", networkServerConfiguration.FacadeServerUrl.ToString());
            Assert.Equal(facadeCode, networkServerConfiguration.FacadeAuthCode);
        }

        [Fact]
        public async Task InitModuleAsync_Update_Should_Perform_Happy_Path()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            var loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            var loRaModuleClient = new Mock<ILoraModuleClient>();
            loRaModuleClient.Setup(x => x.DisposeAsync());
            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;
            var facadeUri = this.faker.Internet.Url();
            var facadeCode = this.faker.Internet.Password();
            var twinProperty = new TwinProperties
            {
                Desired = new TwinCollection($"{{\"FacadeServerUrl\": \"{facadeUri}\", \"FacadeAuthCode\":\"{facadeCode}\"}}")
            };
            loRaModuleClient.Setup(x => x.GetTwinAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Twin(twinProperty));

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, loRaDeviceRegistry.Object, loRaModuleClient.Object);
            await moduleClient.InitModuleAsync(CancellationToken.None);
            Assert.Equal(facadeUri + "/", networkServerConfiguration.FacadeServerUrl.ToString());
            Assert.Equal(facadeCode, networkServerConfiguration.FacadeAuthCode);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("{\"FacadeAuthCode\":\"asdasdada\"}")]
        public async Task InitModuleAsync_Fails_When_Required_Twins_Are_Not_Set(string twin)
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            var loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            var loRaModuleClient = new Mock<ILoraModuleClient>();
            loRaModuleClient.Setup(x => x.DisposeAsync());

            var twinProperty = new TwinProperties
            {
                Desired = new TwinCollection(twin)
            };
            loRaModuleClient.Setup(x => x.GetTwinAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Twin(twinProperty));

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, loRaDeviceRegistry.Object, loRaModuleClient.Object);
            await Assert.ThrowsAsync<ConfigurationErrorsException>(() => moduleClient.InitModuleAsync(CancellationToken.None));
        }

        [Fact]
        public async Task InitModuleAsync_Fails_When_Fail_IoT_Hub_Communication()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            var loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            var loRaModuleClient = new Mock<ILoraModuleClient>();
            loRaModuleClient.Setup(x => x.DisposeAsync());
            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;

            loRaModuleClient.Setup(x => x.GetTwinAsync(It.IsAny<CancellationToken>())).Throws<IotHubCommunicationException>();

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, loRaDeviceRegistry.Object, loRaModuleClient.Object);
            await Assert.ThrowsAsync<IotHubCommunicationException>(() => moduleClient.InitModuleAsync(CancellationToken.None));

        }

        [Fact]
        public async Task OnDirectMethodCall_ClearCache_When_Correct_Should_Work()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            var loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            loRaDeviceRegistry.Setup(x => x.ResetDeviceCache());
            var loRaModuleClient = new Mock<ILoraModuleClient>();
            loRaModuleClient.Setup(x => x.DisposeAsync());
            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, loRaDeviceRegistry.Object, loRaModuleClient.Object);
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
            var loRaModuleClient = new Mock<ILoraModuleClient>();
            loRaModuleClient.Setup(x => x.DisposeAsync());
            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, loRaDeviceRegistry.Object, loRaModuleClient.Object);
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
            var loRaModuleClient = new Mock<ILoraModuleClient>();
            loRaModuleClient.Setup(x => x.DisposeAsync());
            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, loRaDeviceRegistry.Object, loRaModuleClient.Object);

            await Assert.ThrowsAnyAsync<ArgumentNullException>(async () => await moduleClient.OnDirectMethodCalled(null, null));
        }

        [Fact]
        public async Task OnDirectMethodCall_CloudToDeviceDecoderElementName_When_Incorrect_Should_Return_NotFound()
        {
            var networkServerConfiguration = new NetworkServerConfiguration();
            var classCMessageSender = new Mock<IClassCDeviceMessageSender>(MockBehavior.Strict);
            classCMessageSender.Setup(x => x.SendAsync(It.IsAny<ReceivedLoRaCloudToDeviceMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            var loRaModuleClient = new Mock<ILoraModuleClient>();
            loRaModuleClient.Setup(x => x.DisposeAsync());
            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;

            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, loRaDeviceRegistry.Object, loRaModuleClient.Object);
            var c2d = "{\"test\":\"asd\"}";

            var response = await moduleClient.OnDirectMethodCalled(new Microsoft.Azure.Devices.Client.MethodRequest("asd", Encoding.UTF8.GetBytes(c2d)), null);
            Assert.Equal((int)HttpStatusCode.NotFound, response.Status);
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
            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, loRaDeviceRegistry.Object, loRaModuleClient.Object);

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
            var loRaModuleClient = new Mock<ILoraModuleClient>();
            loRaModuleClient.Setup(x => x.DisposeAsync());
            // Change the iot edge timeout.
            networkServerConfiguration.IoTEdgeTimeout = 5;
            await using var moduleClient = new ModuleConnectionHost(networkServerConfiguration, classCMessageSender.Object, loRaDeviceRegistry.Object, loRaModuleClient.Object);

            var response = await moduleClient.OnDirectMethodCalled(new Microsoft.Azure.Devices.Client.MethodRequest(Constants.CloudToDeviceDecoderElementName, Encoding.UTF8.GetBytes(faker.Random.String2(10))), null);
            Assert.Equal((int)HttpStatusCode.BadRequest, response.Status);
        }
    }
}
