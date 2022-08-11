// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq.Expressions;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoraKeysManagerFacade;
    using global::LoRaTools;
    using global::LoRaTools.CommonAPI;
    using global::LoRaTools.IoTHubImpl;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public class SendCloudToDeviceMessageTest
    {
        private const FramePort TestPort = FramePorts.App1;

        private readonly LoRaInMemoryDeviceStore cacheStore;
        private readonly Mock<IServiceClient> serviceClient;
        private readonly Mock<IDeviceRegistryManager> registryManager;
        private readonly Mock<IEdgeDeviceGetter> edgeDeviceGetter;
        private readonly Mock<IChannelPublisher> channelPublisher;
        private readonly SendCloudToDeviceMessage sendCloudToDeviceMessage;

        public SendCloudToDeviceMessageTest()
        {
            this.cacheStore = new LoRaInMemoryDeviceStore();
            this.serviceClient = new Mock<IServiceClient>(MockBehavior.Strict);
            this.registryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);
            this.edgeDeviceGetter = new Mock<IEdgeDeviceGetter>();
            this.channelPublisher = new Mock<IChannelPublisher>();
            this.sendCloudToDeviceMessage = new SendCloudToDeviceMessage(this.cacheStore,
                                                                         this.registryManager.Object,
                                                                         this.serviceClient.Object,
                                                                         this.edgeDeviceGetter.Object,
                                                                         this.channelPublisher.Object,
                                                                         new NullLogger<SendCloudToDeviceMessage>());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task When_DevEUI_Is_Missing_Should_Return_BadRequest(string devEUI)
        {
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                DevEUI = null,
            };

            var request = new DefaultHttpContext().Request;
            request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(c2dMessage)));

            var result = await this.sendCloudToDeviceMessage.Run(request, devEUI, default);

            Assert.IsType<BadRequestObjectResult>(result);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Request_Is_Missing_Should_Return_BadRequest()
        {
            var actual = await this.sendCloudToDeviceMessage.Run(null, new DevEui(123456789).ToString(), default);

            Assert.IsType<BadRequestObjectResult>(actual);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Message_Is_Missing_Should_Return_BadRequest()
        {
            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                new DevEui(123456789),
                null,
                default);

            Assert.IsType<BadRequestObjectResult>(actual);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Message_Is_Invalid_Should_Return_BadRequest()
        {
            var devEui = new DevEui(123456789);
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                DevEUI = devEui,
                Fport = FramePort.MacCommand,
                Payload = "hello",
            };

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEui,
                c2dMessage,
                default);

            Assert.IsType<BadRequestObjectResult>(actual);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task When_Device_Is_Found_In_Cache_Should_Send_Via_Direct_Method_Or_Pub_Sub(bool isEdgeDevice)
        {
            this.edgeDeviceGetter.Setup(m => m.IsEdgeDeviceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(isEdgeDevice);

            var devEui = new DevEui(123456789);
            var preferredGateway = new LoRaDevicePreferredGateway("gateway1", 100);
            LoRaDevicePreferredGateway.SaveToCache(this.cacheStore, devEui, preferredGateway);

            var actualMessage = new LoRaCloudToDeviceMessage()
            {
                Fport = TestPort,
                Payload = "hello",
            };

            LoRaCloudToDeviceMessage receivedC2DMessage = null;

            if (isEdgeDevice)
            {
                this.serviceClient.Setup(x => x.InvokeDeviceMethodAsync("gateway1", Constants.NetworkServerModuleId, It.IsNotNull<CloudToDeviceMethod>(), default))
                    .Callback<string, string, CloudToDeviceMethod, CancellationToken>((device, methodName, method, _) => receivedC2DMessage = JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(method.GetPayloadAsJson()))
                    .ReturnsAsync(new CloudToDeviceMethodResult() { Status = (int)HttpStatusCode.OK });
            }
            else
            {
                this.channelPublisher.Setup(x => x.PublishAsync("gateway1", It.IsNotNull<LnsRemoteCall>()))
                    .Callback<string, LnsRemoteCall>((device, remoteCall) => receivedC2DMessage = JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(remoteCall.JsonData));
            }

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEui,
                actualMessage,
                default);

            Assert.IsType<OkObjectResult>(actual);
            var responseValue = ((OkObjectResult)actual).Value as SendCloudToDeviceMessageResult;
            Assert.NotNull(responseValue);
            Assert.Equal("C", responseValue.ClassType);
            Assert.Equal(devEui, responseValue.DevEui);
            Assert.Null(responseValue.MessageID);
            Assert.Equal(receivedC2DMessage.Fport, actualMessage.Fport);
            Assert.Equal(receivedC2DMessage.Payload, actualMessage.Payload);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Direct_Method_Returns_Error_Code_Should_Forward_Status_Error()
        {
            this.edgeDeviceGetter.Setup(m => m.IsEdgeDeviceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var devEui = new DevEui(0123456789);
            var preferredGateway = new LoRaDevicePreferredGateway("gateway1", 100);
            LoRaDevicePreferredGateway.SaveToCache(this.cacheStore, devEui, preferredGateway);

            this.serviceClient.Setup(x => x.InvokeDeviceMethodAsync("gateway1", Constants.NetworkServerModuleId, It.IsNotNull<CloudToDeviceMethod>(), default))
                .ReturnsAsync(new CloudToDeviceMethodResult() { Status = (int)HttpStatusCode.BadRequest });

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEui,
                new LoRaCloudToDeviceMessage()
                {
                    Fport = TestPort,
                }, default);

            Assert.IsType<ObjectResult>(actual);
            Assert.Equal((int)HttpStatusCode.BadRequest, ((ObjectResult)actual).StatusCode);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Direct_Method_Throws_Exception_Should_Return_Application_Error()
        {
            this.edgeDeviceGetter.Setup(m => m.IsEdgeDeviceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var devEui = new DevEui(123456789);
            var preferredGateway = new LoRaDevicePreferredGateway("gateway1", 100);
            LoRaDevicePreferredGateway.SaveToCache(this.cacheStore, devEui, preferredGateway);

            this.serviceClient.Setup(x => x.InvokeDeviceMethodAsync("gateway1", Constants.NetworkServerModuleId, It.IsNotNull<CloudToDeviceMethod>(), default))
                .ThrowsAsync(new IotHubCommunicationException(string.Empty));

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEui,
                new LoRaCloudToDeviceMessage()
                {
                    Fport = TestPort,
                }, default);

            Assert.IsType<ObjectResult>(actual);
            Assert.Equal((int)HttpStatusCode.InternalServerError, ((ObjectResult)actual).StatusCode);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Does_Not_Have_DevAddr_Should_Return_BadRequest()
        {
            var devEui = new DevEui(123456789);

            var mockDeviceTwin = new Mock<ILoRaDeviceTwin>();
            var mockTwinProperties = SetupMockTwinProperties();

            mockDeviceTwin.SetupGet(c => c.Properties)
                           .Returns(mockTwinProperties.Object);

            var query = new Mock<IRegistryPageResult<ILoRaDeviceTwin>>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextPageAsync())
                .ReturnsAsync(new[] { mockDeviceTwin.Object });

            this.registryManager.Setup(x => x.FindDeviceByDevEUI(It.IsNotNull<DevEui>()))
                .Returns(query.Object);

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEui,
                new LoRaCloudToDeviceMessage()
                {
                    Fport = TestPort,
                }, default);

            Assert.IsType<BadRequestObjectResult>(actual);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }

        [Fact]
        public async Task When_Querying_Devices_Throws_Exception_Should_Return_ApplicationError()
        {
            var devEui = new DevEui(123456789);

            var query = new Mock<IRegistryPageResult<ILoRaDeviceTwin>>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextPageAsync())
                .ThrowsAsync(new IotHubCommunicationException(string.Empty));

            this.registryManager.Setup(x => x.FindDeviceByDevEUI(It.IsNotNull<DevEui>()))
                .Returns(query.Object);

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEui,
                new LoRaCloudToDeviceMessage()
                {
                    Fport = TestPort,
                }, default);

            Assert.IsType<ObjectResult>(actual);
            Assert.Equal((int)HttpStatusCode.InternalServerError, ((ObjectResult)actual).StatusCode);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }

        [Fact]
        public async Task When_Querying_Devices_Is_Empty_Should_Return_NotFound()
        {
            var devEui = new DevEui(123456789);

            var query = new Mock<IRegistryPageResult<ILoRaDeviceTwin>>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextPageAsync())
                .ReturnsAsync(Array.Empty<ILoRaDeviceTwin>());

            this.registryManager.Setup(x => x.FindDeviceByDevEUI(It.IsNotNull<DevEui>()))
                .Returns(query.Object);

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEui,
                new LoRaCloudToDeviceMessage()
                {
                    Fport = TestPort,
                }, default);

            Assert.IsType<NotFoundObjectResult>(actual);
            Assert.Equal((int)HttpStatusCode.NotFound, ((ObjectResult)actual).StatusCode);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task When_Querying_Devices_And_Finds_Class_C_Should_Update_Cache_And_Send_Direct_Method_Or_Pub_Sub(bool isEdgeDevice)
        {
            this.edgeDeviceGetter.Setup(m => m.IsEdgeDeviceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(isEdgeDevice);

            var devEui = new DevEui(123456789);

            var mockDeviceTwin = new Mock<ILoRaDeviceTwin>();

            var mockTwinProperties = SetupMockTwinProperties(
                desired: $"{{\"DevAddr\": \"03010101\", \"ClassType\": \"C\"}}",
                reported: $"{{\"PreferredGatewayID\": \"gateway1\" }}");

            mockDeviceTwin.SetupGet(c => c.Properties)
                .Returns(mockTwinProperties.Object);

            var query = new Mock<IRegistryPageResult<ILoRaDeviceTwin>>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextPageAsync())
                .ReturnsAsync(new[] { mockDeviceTwin.Object });

            this.registryManager.Setup(x => x.FindDeviceByDevEUI(It.IsNotNull<DevEui>()))
                .Returns(query.Object);

            var actualMessage = new LoRaCloudToDeviceMessage()
            {
                Fport = TestPort,
                Payload = "hello",
            };

            LoRaCloudToDeviceMessage receivedC2DMessage = null;

            if (isEdgeDevice)
            {
                this.serviceClient.Setup(x => x.InvokeDeviceMethodAsync("gateway1", Constants.NetworkServerModuleId, It.IsNotNull<CloudToDeviceMethod>(), default))
                    .Callback<string, string, CloudToDeviceMethod, CancellationToken>((device, methodName, method, _) => receivedC2DMessage = JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(method.GetPayloadAsJson()))
                    .ReturnsAsync(new CloudToDeviceMethodResult() { Status = (int)HttpStatusCode.OK });
            }
            else
            {
                this.channelPublisher.Setup(x => x.PublishAsync("gateway1", It.IsNotNull<LnsRemoteCall>()))
                    .Callback<string, LnsRemoteCall>((device, remoteCall) => receivedC2DMessage = JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(remoteCall.JsonData));
            }


            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEui,
                actualMessage, default);

            Assert.IsType<OkObjectResult>(actual);
            var responseValue = ((OkObjectResult)actual).Value as SendCloudToDeviceMessageResult;
            Assert.NotNull(responseValue);
            Assert.Equal("C", responseValue.ClassType);
            Assert.Equal(devEui, responseValue.DevEui);
            Assert.Null(responseValue.MessageID);

            var cachedPreferredGateway = LoRaDevicePreferredGateway.LoadFromCache(this.cacheStore, devEui);
            Assert.NotNull(cachedPreferredGateway);
            Assert.Equal("gateway1", cachedPreferredGateway.GatewayID);
            Assert.Equal(receivedC2DMessage.Fport, actualMessage.Fport);
            Assert.Equal(receivedC2DMessage.Payload, actualMessage.Payload);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }


        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task When_Querying_Devices_And_Finds_Single_Gateway_Class_C_Should_Update_Cache_And_Send_Direct_Method_Or_Pub_Sub(bool isEdgeDevice)
        {
            this.edgeDeviceGetter.Setup(m => m.IsEdgeDeviceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(isEdgeDevice);
            var devEui = new DevEui(123456789);

            var mockDeviceTwin = new Mock<ILoRaDeviceTwin>();

            var mockTwinProperties = SetupMockTwinProperties(desired: $"{{\"DevAddr\": \"{new DevAddr(100)}\", \"ClassType\": \"C\", \"GatewayID\":\"mygateway\"}}");

            mockDeviceTwin.SetupGet(c => c.Properties)
                .Returns(mockTwinProperties.Object);

            var query = new Mock<IRegistryPageResult<ILoRaDeviceTwin>>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextPageAsync())
                .ReturnsAsync(new[] { mockDeviceTwin.Object });

            this.registryManager.Setup(x => x.FindDeviceByDevEUI(It.IsNotNull<DevEui>()))
                .Returns(query.Object);

            var actualMessage = new LoRaCloudToDeviceMessage()
            {
                Fport = TestPort,
                Payload = "hello",
            };

            LoRaCloudToDeviceMessage receivedC2DMessage = null;
            if (isEdgeDevice)
            {
                this.serviceClient.Setup(x => x.InvokeDeviceMethodAsync("mygateway", Constants.NetworkServerModuleId, It.IsNotNull<CloudToDeviceMethod>(), default))
                    .Callback<string, string, CloudToDeviceMethod, CancellationToken>((device, methodName, method, _) => receivedC2DMessage = JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(method.GetPayloadAsJson()))
                    .ReturnsAsync(new CloudToDeviceMethodResult() { Status = (int)HttpStatusCode.OK });
            }
            else
            {
                this.channelPublisher.Setup(x => x.PublishAsync("mygateway", It.IsNotNull<LnsRemoteCall>()))
                    .Callback<string, LnsRemoteCall>((device, remoteCall) => receivedC2DMessage = JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(remoteCall.JsonData));
            }

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEui,
                actualMessage, default);

            Assert.IsType<OkObjectResult>(actual);
            var responseValue = ((OkObjectResult)actual).Value as SendCloudToDeviceMessageResult;
            Assert.NotNull(responseValue);
            Assert.Equal("C", responseValue.ClassType);
            Assert.Equal(devEui, responseValue.DevEui);
            Assert.Null(responseValue.MessageID);

            var cachedPreferredGateway = LoRaDevicePreferredGateway.LoadFromCache(this.cacheStore, devEui);
            Assert.NotNull(cachedPreferredGateway);
            Assert.Equal("mygateway", cachedPreferredGateway.GatewayID);

            Assert.Equal(receivedC2DMessage.Fport, actualMessage.Fport);
            Assert.Equal(receivedC2DMessage.Payload, actualMessage.Payload);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }

        [Fact]
        public async Task When_Querying_Devices_And_Finds_No_Gateway_For_Class_C_Should_Return_InternalServerError()
        {
            var devEui = new DevEui(0123456789);
            var devAddr = new DevAddr(03010101);
            var mockDeviceTwin = new Mock<ILoRaDeviceTwin>();

            var mockTwinProperties = SetupMockTwinProperties(desired: $"{{\"DevAddr\": \"{devAddr}\", \"ClassType\": \"C\"}}");

            mockDeviceTwin.SetupGet(c => c.Properties)
                .Returns(mockTwinProperties.Object);

            var query = new Mock<IRegistryPageResult<ILoRaDeviceTwin>>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextPageAsync())
                .ReturnsAsync(new[] { mockDeviceTwin.Object });

            this.registryManager.Setup(x => x.FindDeviceByDevEUI(It.IsNotNull<DevEui>()))
                .Returns(query.Object);

            var actualMessage = new LoRaCloudToDeviceMessage()
            {
                Fport = TestPort,
                Payload = "hello",
            };

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEui,
                actualMessage, default);

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
            var devEui = new DevEui(123456789);

            var mockDeviceTwin = new Mock<ILoRaDeviceTwin>();

            var mockTwinProperties = SetupMockTwinProperties(desired: $"{{\"DevAddr\": \"03010101\"}}");

            mockDeviceTwin.SetupGet(c => c.Properties)
                .Returns(mockTwinProperties.Object);

            var query = new Mock<IRegistryPageResult<ILoRaDeviceTwin>>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextPageAsync())
                .ReturnsAsync(new[] { mockDeviceTwin.Object });

            this.registryManager.Setup(x => x.FindDeviceByDevEUI(It.IsNotNull<DevEui>()))
                .Returns(query.Object);

            var actualMessage = new LoRaCloudToDeviceMessage()
            {
                MessageId = "myMessageId-1234",
                Fport = TestPort,
                Payload = "hello",
            };

            LoRaCloudToDeviceMessage receivedC2DMessage = null;
            IDictionary<string, string> receivedProperties = null;
            this.serviceClient.Setup(x => x.SendAsync(devEui.ToString(), It.IsNotNull<Message>()))
                .Callback((string d, Message m) => (receivedProperties, receivedC2DMessage) = (m.Properties, JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(Encoding.UTF8.GetString(m.GetBytes()))))
                .Returns(Task.CompletedTask);

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEui,
                actualMessage, default);

            Assert.IsType<OkObjectResult>(actual);
            var responseValue = ((OkObjectResult)actual).Value as SendCloudToDeviceMessageResult;
            Assert.NotNull(responseValue);
            Assert.Equal("A", responseValue.ClassType);
            Assert.Equal(devEui, responseValue.DevEui);
            Assert.Equal(actualMessage.MessageId, responseValue.MessageID);

            Assert.Empty(receivedProperties);
            Assert.Equal(receivedC2DMessage.Fport, actualMessage.Fport);
            Assert.Equal(receivedC2DMessage.Payload, actualMessage.Payload);
            Assert.Equal(receivedC2DMessage.MessageId, actualMessage.MessageId);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }

        [Fact]
        public async Task When_Sending_Message_Throws_Error_Should_Return_Application_Error()
        {
            var devEui = new DevEui(123456789);

            var mockDeviceTwin = new Mock<ILoRaDeviceTwin>();

            var mockTwinProperties = SetupMockTwinProperties(desired: $"{{\"DevAddr\": \"03010101\"}}");

            mockDeviceTwin.SetupGet(c => c.Properties)
                .Returns(mockTwinProperties.Object);

            var query = new Mock<IRegistryPageResult<ILoRaDeviceTwin>>(MockBehavior.Strict);
            query.Setup(x => x.HasMoreResults).Returns(true);
            query.Setup(x => x.GetNextPageAsync())
                .ReturnsAsync(new[] { mockDeviceTwin.Object });

            this.registryManager.Setup(x => x.FindDeviceByDevEUI(It.IsNotNull<DevEui>()))
                .Returns(query.Object);

            var actualMessage = new LoRaCloudToDeviceMessage()
            {
                MessageId = "myMessageId-1234",
                Fport = TestPort,
                Payload = "hello",
            };

            this.serviceClient.Setup(x => x.SendAsync(devEui.ToString(), It.IsNotNull<Message>()))
                .ThrowsAsync(new IotHubCommunicationException(string.Empty));

            var actual = await this.sendCloudToDeviceMessage.SendCloudToDeviceMessageImplementationAsync(
                devEui,
                actualMessage, default);

            Assert.IsType<ObjectResult>(actual);
            Assert.Equal((int)HttpStatusCode.InternalServerError, ((ObjectResult)actual).StatusCode);

            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
            query.VerifyAll();
        }

        private static Mock<ITwinPropertiesContainer> SetupMockTwinProperties(string desired = "{}", string reported = "{}")
        {
            ArgumentNullException.ThrowIfNull(desired, nameof(desired));
            ArgumentNullException.ThrowIfNull(reported, nameof(reported));

            var mockDesiredProperties = SetupMockTwinProperties(desired);
            var mockReportedProperties = SetupMockTwinProperties(reported);

            var mockTwinProperties = new Mock<ITwinPropertiesContainer>();

            mockTwinProperties.SetupGet(c => c.Desired)
                .Returns(mockDesiredProperties.Object);

            mockTwinProperties.SetupGet(c => c.Reported)
                .Returns(mockReportedProperties.Object);

            return mockTwinProperties;
        }

        private static Mock<ITwinProperties> SetupMockTwinProperties(string properties)
        {
            var mockProperties = new Mock<ITwinProperties>();

            var propertiesObject = JObject.Parse(properties);

            mockProperties.Setup(c => c[It.IsAny<string>()])
                            .Returns((string propertyName) => propertiesObject[propertyName]);

            mockProperties.Setup(c => c.ContainsKey(It.IsAny<string>()))
                .Returns((string c) => propertiesObject.ContainsKey(c));

            _ = mockProperties.Setup(c => c.TryGetValue(It.IsAny<string>(), out It.Ref<object>.IsAny))
                .Returns((string parameterName, out object outval) =>
                {
                    var tryGetValueResult = propertiesObject.TryGetValue(parameterName, out var token);
                    outval = token;

                    return tryGetValueResult;
                });

            return mockProperties;
        }
    }
}
