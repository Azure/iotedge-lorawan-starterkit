// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using global::LoraKeysManagerFacade;
    using global::LoRaTools.CommonAPI;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    public class SendCloudToDeviceMessageTest
    {
        private const FramePort TestPort = FramePorts.App1;

        private readonly LoRaInMemoryDeviceStore cacheStore;
        private readonly Mock<IServiceClient> serviceClient;
        private readonly Mock<RegistryManager> registryManager;
        private readonly SendCloudToDeviceMessage sendCloudToDeviceMessage;

        public SendCloudToDeviceMessageTest()
        {
            this.cacheStore = new LoRaInMemoryDeviceStore();
            this.serviceClient = new Mock<IServiceClient>(MockBehavior.Strict);
            this.registryManager = new Mock<RegistryManager>(MockBehavior.Strict);
            this.sendCloudToDeviceMessage = new SendCloudToDeviceMessage(this.cacheStore, this.registryManager.Object, this.serviceClient.Object, new NullLogger<SendCloudToDeviceMessage>());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task When_DevEUI_Is_Missing_Should_Return_BadRequest(string devEUI)
        {
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                DevEUI = devEUI,
            };

            var request = new DefaultHttpContext().Request;
            request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(c2dMessage)));

            var result = await this.sendCloudToDeviceMessage.Run(request, devEUI);

            Assert.IsType<BadRequestObjectResult>(result);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Request_Is_Missing_Should_Return_BadRequest()
        {
            var actual = await this.sendCloudToDeviceMessage.Run(null, "0123456789");

            Assert.IsType<BadRequestObjectResult>(actual);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Message_Is_Missing_Should_Return_BadRequest()
        {
            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                "0123456789",
                null);

            Assert.IsType<BadRequestObjectResult>(actual);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Message_Is_Invalid_Should_Return_BadRequest()
        {
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                DevEUI = "0123456789",
                Fport = FramePort.MacCommand,
                Payload = "hello",
            };

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                "0123456789",
                c2dMessage);

            Assert.IsType<BadRequestObjectResult>(actual);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Found_In_Cache_Should_Send_Via_Direct_Method()
        {
            const string devEUI = "0123456789";
            var preferredGateway = new LoRaDevicePreferredGateway("gateway1", 100);
            LoRaDevicePreferredGateway.SaveToCache(this.cacheStore, devEUI, preferredGateway);

            var actualMessage = new LoRaCloudToDeviceMessage()
            {
                Fport = TestPort,
                Payload = "hello",
            };

            this.serviceClient.Setup(x => x.InvokeDeviceMethodAsync("gateway1", LoraKeysManagerFacadeConstants.NetworkServerModuleId, It.IsNotNull<CloudToDeviceMethod>()))
                .Callback<string, string, CloudToDeviceMethod>((device, methodName, method) =>
                {
                    var c2dMessage = JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(method.GetPayloadAsJson());
                    Assert.Equal(c2dMessage.Fport, actualMessage.Fport);
                    Assert.Equal(c2dMessage.Payload, actualMessage.Payload);
                })
                .ReturnsAsync(new CloudToDeviceMethodResult() { Status = (int)HttpStatusCode.OK });

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEUI,
                actualMessage);

            Assert.IsType<OkObjectResult>(actual);
            var responseValue = ((OkObjectResult)actual).Value as SendCloudToDeviceMessageResult;
            Assert.NotNull(responseValue);
            Assert.Equal("C", responseValue.ClassType);
            Assert.Equal(devEUI, responseValue.DevEUI);
            Assert.Null(responseValue.MessageID);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Direct_Method_Returns_Error_Code_Should_Forward_Status_Error()
        {
            const string devEUI = "0123456789";
            var preferredGateway = new LoRaDevicePreferredGateway("gateway1", 100);
            LoRaDevicePreferredGateway.SaveToCache(this.cacheStore, devEUI, preferredGateway);

            this.serviceClient.Setup(x => x.InvokeDeviceMethodAsync("gateway1", LoraKeysManagerFacadeConstants.NetworkServerModuleId, It.IsNotNull<CloudToDeviceMethod>()))
                .ReturnsAsync(new CloudToDeviceMethodResult() { Status = (int)HttpStatusCode.BadRequest });

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEUI,
                new LoRaCloudToDeviceMessage()
                {
                    Fport = TestPort,
                });

            Assert.IsType<ObjectResult>(actual);
            Assert.Equal((int)HttpStatusCode.BadRequest, ((ObjectResult)actual).StatusCode);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Direct_Method_Throws_Exception_Should_Return_Application_Error()
        {
            const string devEUI = "0123456789";
            var preferredGateway = new LoRaDevicePreferredGateway("gateway1", 100);
            LoRaDevicePreferredGateway.SaveToCache(this.cacheStore, devEUI, preferredGateway);

            this.serviceClient.Setup(x => x.InvokeDeviceMethodAsync("gateway1", LoraKeysManagerFacadeConstants.NetworkServerModuleId, It.IsNotNull<CloudToDeviceMethod>()))
                .ThrowsAsync(new IotHubCommunicationException(string.Empty));

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEUI,
                new LoRaCloudToDeviceMessage()
                {
                    Fport = TestPort,
                });

            Assert.IsType<ObjectResult>(actual);
            Assert.Equal((int)HttpStatusCode.InternalServerError, ((ObjectResult)actual).StatusCode);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Does_Not_Have_DevAddr_Should_Return_BadRequest()
        {
            const string devEUI = "0123456789";

            var query = new Mock<IQuery>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextAsTwinAsync())
                .ReturnsAsync(new[] { new Twin() });

            this.registryManager.Setup(x => x.CreateQuery(It.IsNotNull<string>(), It.IsAny<int?>()))
                .Returns(query.Object);

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEUI,
                new LoRaCloudToDeviceMessage()
                {
                    Fport = TestPort,
                });

            Assert.IsType<BadRequestObjectResult>(actual);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }

        [Fact]
        public async Task When_Querying_Devices_Throws_Exception_Should_Return_ApplicationError()
        {
            const string devEUI = "0123456789";

            var query = new Mock<IQuery>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextAsTwinAsync())
                .ThrowsAsync(new IotHubCommunicationException(string.Empty));

            this.registryManager.Setup(x => x.CreateQuery(It.IsNotNull<string>(), It.IsAny<int?>()))
                .Returns(query.Object);

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEUI,
                new LoRaCloudToDeviceMessage()
                {
                    Fport = TestPort,
                });

            Assert.IsType<ObjectResult>(actual);
            Assert.Equal((int)HttpStatusCode.InternalServerError, ((ObjectResult)actual).StatusCode);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }

        [Fact]
        public async Task When_Querying_Devices_Is_Empty_Should_Return_NotFound()
        {
            const string devEUI = "0123456789";

            var query = new Mock<IQuery>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextAsTwinAsync())
                .ReturnsAsync(Array.Empty<Twin>());

            this.registryManager.Setup(x => x.CreateQuery(It.IsNotNull<string>(), It.IsAny<int?>()))
                .Returns(query.Object);

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEUI,
                new LoRaCloudToDeviceMessage()
                {
                    Fport = TestPort,
                });

            Assert.IsType<NotFoundObjectResult>(actual);
            Assert.Equal((int)HttpStatusCode.NotFound, ((ObjectResult)actual).StatusCode);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }

        [Fact]
        public async Task When_Querying_Devices_And_Finds_Class_C_Should_Update_Cache_And_Send_Direct_Method()
        {
            const string devEUI = "0123456789";

            var deviceTwin = new Twin
            {
                Properties = new TwinProperties()
                {
                    Desired = new TwinCollection($"{{\"DevAddr\": \"03010101\", \"ClassType\": \"C\"}}"),
                    Reported = new TwinCollection($"{{\"PreferredGatewayID\": \"gateway1\" }}"),
                }
            };

            var query = new Mock<IQuery>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextAsTwinAsync())
                .ReturnsAsync(new[] { deviceTwin });

            this.registryManager.Setup(x => x.CreateQuery(It.IsNotNull<string>(), It.IsAny<int?>()))
                .Returns(query.Object);

            var actualMessage = new LoRaCloudToDeviceMessage()
            {
                Fport = TestPort,
                Payload = "hello",
            };

            this.serviceClient.Setup(x => x.InvokeDeviceMethodAsync("gateway1", LoraKeysManagerFacadeConstants.NetworkServerModuleId, It.IsNotNull<CloudToDeviceMethod>()))
                .Callback<string, string, CloudToDeviceMethod>((device, methodName, method) =>
                {
                    var c2dMessage = JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(method.GetPayloadAsJson());
                    Assert.Equal(c2dMessage.Fport, actualMessage.Fport);
                    Assert.Equal(c2dMessage.Payload, actualMessage.Payload);
                })
                .ReturnsAsync(new CloudToDeviceMethodResult() { Status = (int)HttpStatusCode.OK });

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEUI,
                actualMessage);

            Assert.IsType<OkObjectResult>(actual);
            var responseValue = ((OkObjectResult)actual).Value as SendCloudToDeviceMessageResult;
            Assert.NotNull(responseValue);
            Assert.Equal("C", responseValue.ClassType);
            Assert.Equal(devEUI, responseValue.DevEUI);
            Assert.Null(responseValue.MessageID);

            var cachedPreferredGateway = LoRaDevicePreferredGateway.LoadFromCache(this.cacheStore, devEUI);
            Assert.NotNull(cachedPreferredGateway);
            Assert.Equal("gateway1", cachedPreferredGateway.GatewayID);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }

        [Fact]
        public async Task When_Querying_Devices_And_Finds_Single_Gateway_Class_C_Should_Update_Cache_And_Send_Direct_Method()
        {
            const string devEUI = "0123456789";

            var deviceTwin = new Twin
            {
                Properties = new TwinProperties()
                {
                    Desired = new TwinCollection($"{{\"DevAddr\": \"{new DevAddr(100)}\", \"ClassType\": \"C\", \"GatewayID\":\"mygateway\"}}"),
                    Reported = new TwinCollection(),
                }
            };

            var query = new Mock<IQuery>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextAsTwinAsync())
                .ReturnsAsync(new[] { deviceTwin });

            this.registryManager.Setup(x => x.CreateQuery(It.IsNotNull<string>(), It.IsAny<int?>()))
                .Returns(query.Object);

            var actualMessage = new LoRaCloudToDeviceMessage()
            {
                Fport = TestPort,
                Payload = "hello",
            };

            this.serviceClient.Setup(x => x.InvokeDeviceMethodAsync("mygateway", LoraKeysManagerFacadeConstants.NetworkServerModuleId, It.IsNotNull<CloudToDeviceMethod>()))
                .Callback<string, string, CloudToDeviceMethod>((device, methodName, method) =>
                {
                    var c2dMessage = JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(method.GetPayloadAsJson());
                    Assert.Equal(c2dMessage.Fport, actualMessage.Fport);
                    Assert.Equal(c2dMessage.Payload, actualMessage.Payload);
                })
                .ReturnsAsync(new CloudToDeviceMethodResult() { Status = (int)HttpStatusCode.OK });

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEUI,
                actualMessage);

            Assert.IsType<OkObjectResult>(actual);
            var responseValue = ((OkObjectResult)actual).Value as SendCloudToDeviceMessageResult;
            Assert.NotNull(responseValue);
            Assert.Equal("C", responseValue.ClassType);
            Assert.Equal(devEUI, responseValue.DevEUI);
            Assert.Null(responseValue.MessageID);

            var cachedPreferredGateway = LoRaDevicePreferredGateway.LoadFromCache(this.cacheStore, devEUI);
            Assert.NotNull(cachedPreferredGateway);
            Assert.Equal("mygateway", cachedPreferredGateway.GatewayID);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }

        [Fact]
        public async Task When_Querying_Devices_And_Finds_No_Gateway_For_Class_C_Should_Return_InternalServerError()
        {
            var devEUI = new DevEui(0123456789);
            var devAddr = new DevAddr(03010101);
            var deviceTwin = new Twin
            {
                Properties = new TwinProperties()
                {
                    Desired = new TwinCollection($"{{\"DevAddr\": \"{devAddr}\", \"ClassType\": \"C\"}}"),
                    Reported = new TwinCollection(),
                }
            };

            var query = new Mock<IQuery>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextAsTwinAsync())
                .ReturnsAsync(new[] { deviceTwin });

            this.registryManager.Setup(x => x.CreateQuery(It.IsNotNull<string>(), It.IsAny<int?>()))
                .Returns(query.Object);

            var actualMessage = new LoRaCloudToDeviceMessage()
            {
                Fport = TestPort,
                Payload = "hello",
            };

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEUI.ToString(),
                actualMessage);

            var result = Assert.IsType<ObjectResult>(actual);
            Assert.Equal(500, result.StatusCode);
            Assert.Equal("Class C devices must sent at least one message upstream. None has been received", result.Value.ToString());

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }

        [Fact]
        public async Task When_Querying_Devices_And_Finds_Class_A_Should_Send_Message()
        {
            const string devEUI = "0123456789";

            var deviceTwin = new Twin
            {
                Properties = new TwinProperties()
                {
                    Desired = new TwinCollection($"{{\"DevAddr\": \"03010101\"}}"),
                }
            };

            var query = new Mock<IQuery>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextAsTwinAsync())
                .ReturnsAsync(new[] { deviceTwin });

            this.registryManager.Setup(x => x.CreateQuery(It.IsNotNull<string>(), It.IsAny<int?>()))
                .Returns(query.Object);

            var actualMessage = new LoRaCloudToDeviceMessage()
            {
                MessageId = "myMessageId-1234",
                Fport = TestPort,
                Payload = "hello",
            };

            this.serviceClient.Setup(x => x.SendAsync(devEUI, It.IsNotNull<Message>()))
                .Callback<string, Message>((d, m) =>
                {
                    Assert.Empty(m.Properties);
                    var c2dMessage = JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(Encoding.UTF8.GetString(m.GetBytes()));
                    Assert.Equal(c2dMessage.Fport, actualMessage.Fport);
                    Assert.Equal(c2dMessage.Payload, actualMessage.Payload);
                    Assert.Equal(c2dMessage.MessageId, actualMessage.MessageId);
                })
                .Returns(Task.CompletedTask);

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEUI,
                actualMessage);

            Assert.IsType<OkObjectResult>(actual);
            var responseValue = ((OkObjectResult)actual).Value as SendCloudToDeviceMessageResult;
            Assert.NotNull(responseValue);
            Assert.Equal("A", responseValue.ClassType);
            Assert.Equal(devEUI, responseValue.DevEUI);
            Assert.Equal(actualMessage.MessageId, responseValue.MessageID);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }

        [Fact]
        public async Task When_Sending_Message_Throws_Error_Should_Return_Application_Error()
        {
            const string devEUI = "0123456789";

            var deviceTwin = new Twin
            {
                Properties = new TwinProperties()
                {
                    Desired = new TwinCollection($"{{\"DevAddr\": \"03010101\"}}"),
                }
            };

            var query = new Mock<IQuery>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextAsTwinAsync())
                .ReturnsAsync(new[] { deviceTwin });

            this.registryManager.Setup(x => x.CreateQuery(It.IsNotNull<string>(), It.IsAny<int?>()))
                .Returns(query.Object);

            var actualMessage = new LoRaCloudToDeviceMessage()
            {
                MessageId = "myMessageId-1234",
                Fport = TestPort,
                Payload = "hello",
            };

            this.serviceClient.Setup(x => x.SendAsync(devEUI, It.IsNotNull<Message>()))
                .ThrowsAsync(new IotHubCommunicationException(string.Empty));

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEUI,
                actualMessage);

            Assert.IsType<ObjectResult>(actual);
            Assert.Equal((int)HttpStatusCode.InternalServerError, ((ObjectResult)actual).StatusCode);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }
    }
}
