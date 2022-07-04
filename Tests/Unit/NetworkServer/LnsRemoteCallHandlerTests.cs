// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Bogus;
    using global::LoRaTools;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public sealed class LnsRemoteCallHandlerTests
    {
        private readonly Faker faker = new();
        private readonly NetworkServerConfiguration networkServerConfiguration;
        private readonly Mock<IClassCDeviceMessageSender> classCMessageSender;
        private readonly Mock<ILoRaDeviceRegistry> loRaDeviceRegistry;
        private readonly Mock<ILogger<LnsRemoteCallHandler>> logger;
        private readonly LnsRemoteCallHandler subject;

        public LnsRemoteCallHandlerTests()
        {
            this.networkServerConfiguration = new NetworkServerConfiguration();
            this.classCMessageSender = new Mock<IClassCDeviceMessageSender>();
            this.loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>();
            this.logger = new Mock<ILogger<LnsRemoteCallHandler>>();
            this.subject = new LnsRemoteCallHandler(this.networkServerConfiguration,
                                            this.classCMessageSender.Object,
                                            this.loRaDeviceRegistry.Object,
                                            this.logger.Object,
                                            TestMeter.Instance);
        }


        [Fact]
        public async Task CloseConnectionAsync_Should_Work_As_Expected()
        {
            // arrange
            var devEui = new DevEui(0);
            var mockedDevice = new Mock<LoRaDevice>(null, devEui, null);
            _ = this.loRaDeviceRegistry.Setup(x => x.GetDeviceByDevEUIAsync(devEui)).ReturnsAsync(mockedDevice.Object);
            var c2d = JsonSerializer.Serialize(new
            {
                DevEui = devEui.ToString(),
                Fport = 1,
                MessageId = Guid.NewGuid(),
            });

            // act
            _ = await CloseConnectionAsync(c2d);

            // assert
            this.loRaDeviceRegistry.VerifyAll();
            mockedDevice.Verify(x => x.CloseConnectionAsync(It.IsAny<CancellationToken>(), true), Times.Once);
        }

        [Fact]
        public async Task ClearCache_When_Correct_Should_Work()
        {
            // arrange
            this.loRaDeviceRegistry.Setup(x => x.ResetDeviceCacheAsync()).Returns(Task.CompletedTask);
            this.networkServerConfiguration.IoTEdgeTimeout = 5;

            // act
            await this.subject.ExecuteAsync(new LnsRemoteCall(RemoteCallKind.ClearCache, null), CancellationToken.None);

            // assert
            this.loRaDeviceRegistry.VerifyAll();
        }

        public static TheoryData<string, string> DropConnectionInvalidMessages =>
            TheoryDataFactory.From(
                (string.Empty, "Unable to parse Json when attempting to close"),
                ("null", "Missing payload when attempting to close the"),
                (JsonSerializer.Serialize(new { DevEui = (string)null, Fport = 1 }), "DevEUI missing"),
                (JsonSerializer.Serialize(new { DevEui = new DevEui(0).ToString(), Fport = 1, MessageId = 123 }), "Unable to parse Json"));

        [Theory]
        [MemberData(nameof(DropConnectionInvalidMessages))]
        public async Task CloseConnectionAsync_Should_Return_Bad_Request_When_Invalid_Message(string json, string expectedLogPattern)
        {
            // act
            var response = await CloseConnectionAsync(json);

            // assert
            Assert.Equal(HttpStatusCode.BadRequest, response);
            var log = Assert.Single(this.logger.GetLogInvocations());
            Assert.Matches(expectedLogPattern, log.Message);
            this.loRaDeviceRegistry.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CloseConnectionAsync_Should_Return_NotFound_When_Device_Not_Found()
        {
            // arrange
            var devEui = new DevEui(0);
            var c2d = JsonSerializer.Serialize(new { DevEui = devEui.ToString(), Fport = 1 });

            // act
            var response = await CloseConnectionAsync(c2d);

            // assert
            Assert.Equal(HttpStatusCode.NotFound, response);
            this.loRaDeviceRegistry.Verify(x => x.GetDeviceByDevEUIAsync(devEui), Times.Once);
            this.loRaDeviceRegistry.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SendCloudToDeviceMessageAsync_When_Correct_Should_Work()
        {
            // arrange
            this.classCMessageSender.Setup(x => x.SendAsync(It.IsAny<ReceivedLoRaCloudToDeviceMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var c2d = "{\"test\":\"asd\"}";

            // act
            var response = await SendCloudToDeviceMessageAsync(c2d);

            // assert
            Assert.Equal(HttpStatusCode.OK, response);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task SendCloudToDeviceMessageAsync_When_ClassC_Msg_Is_Null_Or_Empty_Should_Return_Not_Found(string json)
        {
            this.classCMessageSender.Setup(x => x.SendAsync(It.IsAny<ReceivedLoRaCloudToDeviceMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var response = await SendCloudToDeviceMessageAsync(json);
            Assert.Equal(HttpStatusCode.BadRequest, response);
        }

        [Fact]
        public async Task SendCloudToDeviceMessageAsync_When_ClassC_Msg_Is_Not_CorrectJson_Should_Return_Not_Found()
        {
            var response = await SendCloudToDeviceMessageAsync(this.faker.Random.String2(10));
            Assert.Equal(HttpStatusCode.BadRequest, response);
        }

        private Task<HttpStatusCode> CloseConnectionAsync(string payload) =>
            this.subject.ExecuteAsync(new LnsRemoteCall(RemoteCallKind.CloseConnection, payload), CancellationToken.None);

        private Task<HttpStatusCode> SendCloudToDeviceMessageAsync(string payload) =>
            this.subject.ExecuteAsync(new LnsRemoteCall(RemoteCallKind.CloudToDeviceMessage, payload), CancellationToken.None);
    }
}
