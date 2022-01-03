// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using System.Net;
    using System.Threading.Tasks;
    using global::LoraKeysManagerFacade;
    using global::LoRaTools.CommonAPI;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    public class SendMessageToClassCDeviceTest
    {
        private const string TestDevEUI = "B827EBFFFFF30000";
        private const FramePort TestPort = FramePorts.App1;

        private readonly Mock<IServiceClient> serviceClient;
        private readonly Mock<RegistryManager> registryManager;
        private readonly SendMessageToClassCDevice sendMessageToClassCDevice;

        public SendMessageToClassCDeviceTest()
        {
            this.serviceClient = new Mock<IServiceClient>(MockBehavior.Strict);
            this.registryManager = new Mock<RegistryManager>(MockBehavior.Strict);
            this.sendMessageToClassCDevice = new SendMessageToClassCDevice(this.registryManager.Object, this.serviceClient.Object, new NullLogger<SendMessageToClassCDevice>());
        }

        [Fact]
        public async Task When_Request_Is_Missing_Should_Return_BadRequest()
        {
            var result = await this.sendMessageToClassCDevice.RunSendMessageToClassCDevice("0123456789", null);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Request with message content is required", ((BadRequestObjectResult)result).Value.ToString());

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task When_DevEUI_Is_Missing_Should_Return_BadRequest(string devEUI)
        {
            var message = new LoRaCloudToDeviceMessage();
            var request = HttpRequestHelper.CreateRequest(JsonConvert.SerializeObject(message));

            var result = await this.sendMessageToClassCDevice.RunSendMessageToClassCDevice(devEUI, request);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Invalid DevEUI", ((BadRequestObjectResult)result).Value.ToString(), System.StringComparison.Ordinal);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Message_Is_Invalid_Should_Return_BadRequest()
        {
            // FPort indicates message contains MAC commands but it has payload instead
            var message = new LoRaCloudToDeviceMessage()
            {
                DevEUI = TestDevEUI,
                Fport = FramePort.MacCommand,
                Payload = "payload"
            };
            var request = HttpRequestHelper.CreateRequest(JsonConvert.SerializeObject(message));

            var result = await this.sendMessageToClassCDevice.RunSendMessageToClassCDevice(TestDevEUI, request);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Invalid MAC command fport usage in cloud to device message",
                ((BadRequestObjectResult)result).Value.ToString(), System.StringComparison.OrdinalIgnoreCase);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Message_Has_No_Payload_Should_Return_BadRequest()
        {
            var message = new LoRaCloudToDeviceMessage()
            {
                DevEUI = TestDevEUI,
                Fport = FramePort.AppMin,
            };
            var request = HttpRequestHelper.CreateRequest(JsonConvert.SerializeObject(message));

            var result = await this.sendMessageToClassCDevice.RunSendMessageToClassCDevice(TestDevEUI, request);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Payload is required", ((BadRequestObjectResult)result).Value.ToString());

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Class_C_Device_Found_Should_Send_Direct_Method()
        {
            var deviceTwin = new Twin
            {
                Properties = new TwinProperties()
                {
                    Desired = new TwinCollection($"{{\"DevAddr\": \"03010101\", \"ClassType\": \"C\"}}"),
                    Reported = new TwinCollection($"{{\"PreferredGatewayID\": \"gateway1\" }}"),
                }
            };

            this.registryManager.Setup(x => x.GetTwinAsync(It.IsNotNull<string>()))
                .ReturnsAsync(deviceTwin);

            var message = new LoRaCloudToDeviceMessage()
            {
                Fport = TestPort,
                Payload = "payload",
            };

            this.serviceClient.Setup(x => x.InvokeDeviceMethodAsync("gateway1", LoraKeysManagerFacadeConstants.NetworkServerModuleId, It.IsNotNull<CloudToDeviceMethod>()))
                .Callback<string, string, CloudToDeviceMethod>((device, methodName, method) =>
                {
                    var c2dMessage = JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(method.GetPayloadAsJson());
                    Assert.Equal(c2dMessage.Fport, message.Fport);
                    Assert.Equal(c2dMessage.Payload, message.Payload);
                })
                .ReturnsAsync(new CloudToDeviceMethodResult() { Status = (int)HttpStatusCode.OK });

            var request = HttpRequestHelper.CreateRequest(JsonConvert.SerializeObject(message));
            var result = await this.sendMessageToClassCDevice.RunSendMessageToClassCDevice(TestDevEUI, request);

            Assert.IsType<OkObjectResult>(result);
            var responseValue = ((OkObjectResult)result).Value as SendCloudToDeviceMessageResult;
            Assert.NotNull(responseValue);
            Assert.Equal("C", responseValue.ClassType);
            Assert.Equal(TestDevEUI, responseValue.DevEUI);
            Assert.Null(responseValue.MessageID);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }
    }
}
