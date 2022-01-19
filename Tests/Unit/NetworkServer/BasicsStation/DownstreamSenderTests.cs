// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer.BasicsStation
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools.LoRaPhysical;
    using global::LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;
    using static RxDelay;

    public class DownstreamSenderTests
    {
        private const string loraDataBase64 = "REFUQQ==";
        private readonly StationEui stationEui = new StationEui(ulong.MaxValue);
        private readonly DevEui devEui = new DevEui(ulong.MaxValue);
        private readonly Mock<IWebSocketWriter<string>> webSocketWriter;
        private readonly byte[] loraDataByteArray;
        private readonly DownstreamSender downlinkSender;

        public DownstreamSenderTests()
        {
            var socketWriterRegistry = new WebSocketWriterRegistry<StationEui, string>(Mock.Of<ILogger<WebSocketWriterRegistry<StationEui, string>>>(), null);
            this.webSocketWriter = new Mock<IWebSocketWriter<string>>();

            var basicStationConfigurationService = new Mock<IBasicsStationConfigurationService>();
            basicStationConfigurationService.Setup(x => x.GetRegionAsync(stationEui, It.IsAny<CancellationToken>()))
                                            .Returns(Task.FromResult(RegionManager.EU868));

            socketWriterRegistry.Register(stationEui, this.webSocketWriter.Object);

            loraDataByteArray = Encoding.UTF8.GetBytes(loraDataBase64);

            downlinkSender = new DownstreamSender(socketWriterRegistry,
                                                  basicStationConfigurationService.Object,
                                                  Mock.Of<ILogger<DownstreamSender>>());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SendDownstreamAsync_Succeeds_WithValidDownlinkMessage_ClassADevice(bool rfchHasValue)
        {
            // arrange
            var downlinkMessage = new DownlinkMessage(this.loraDataByteArray,
                                                      123456,
                                                      (DataRateIndex.DR5, Hertz.Mega(868.5)),
                                                      (DataRateIndex.DR0, Hertz.Mega(869.5)),
                                                      this.devEui,
                                                      RxDelay1,
                                                      LoRaDeviceClassType.A,
                                                      this.stationEui,
                                                      rfchHasValue ? 1 : null);

            var actualMessage = string.Empty;
            this.webSocketWriter.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                .Callback<string, CancellationToken>((message, _) =>
                                {
                                    actualMessage = message;
                                });
            // act
            await downlinkSender.SendDownstreamAsync(downlinkMessage);

            // assert
            Assert.NotEmpty(actualMessage);
            Assert.Contains(@"""msgtype"":""dnmsg""", actualMessage, StringComparison.InvariantCulture);
            Assert.Contains(@"""DevEui"":""FFFFFFFFFFFFFFFF"",", actualMessage, StringComparison.InvariantCulture);
            Assert.Contains(@"""dC"":0", actualMessage, StringComparison.InvariantCulture);
            Assert.Contains(@"""pdu"":""5245465551513D3D"",", actualMessage, StringComparison.InvariantCulture);
            Assert.Contains(@"""RxDelay"":1,", actualMessage, StringComparison.InvariantCulture);
            Assert.Contains(@"""RX1DR"":5,", actualMessage, StringComparison.InvariantCulture);
            Assert.Contains(@"""RX1Freq"":868500000,", actualMessage, StringComparison.InvariantCulture);
            Assert.Contains(@"""RX2DR"":0,", actualMessage, StringComparison.InvariantCulture);
            Assert.Contains(@"""RX2Freq"":869500000,", actualMessage, StringComparison.InvariantCulture);
            Assert.Contains(@"""xtime"":123456,", actualMessage, StringComparison.InvariantCulture);

            Assert.Contains(@"""priority"":0", actualMessage, StringComparison.InvariantCulture);

            if (rfchHasValue)
            {
                Assert.Contains(@"""rctx"":1", actualMessage, StringComparison.InvariantCulture);
            }
            else
            {
                Assert.DoesNotContain("rctx", actualMessage, StringComparison.InvariantCulture);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SendDownstreamAsync_Succeeds_WithValidDownlinkMessage_ClassCDevice(bool rfchHasValue)
        {
            // arrange
            var downlinkMessage = new DownlinkMessage(this.loraDataByteArray,
                                                      0,
                                                      (DataRateIndex.DR5, Hertz.Mega(868.5)),
                                                      (DataRateIndex.DR0, Hertz.Mega(869.5)),
                                                      this.devEui,
                                                      RxDelay0,
                                                      LoRaDeviceClassType.C,
                                                      this.stationEui,
                                                      rfchHasValue ? 1 : null);

            var actualMessage = string.Empty;
            this.webSocketWriter.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                .Callback<string, CancellationToken>((message, _) =>
                                {
                                    actualMessage = message;
                                });
            // act
            await downlinkSender.SendDownstreamAsync(downlinkMessage);

            // assert
            Assert.NotEmpty(actualMessage);
            Assert.Contains(@"""msgtype"":""dnmsg"",", actualMessage, StringComparison.InvariantCulture);
            Assert.Contains(@"""DevEui"":""FFFFFFFFFFFFFFFF"",", actualMessage, StringComparison.InvariantCulture);
            Assert.Contains(@"""dC"":2,", actualMessage, StringComparison.InvariantCulture);
            Assert.Contains(@"""pdu"":""5245465551513D3D"",", actualMessage, StringComparison.InvariantCulture);
            // Will select DR0 as it is the second DR.
            Assert.Contains(@"""RX2DR"":0,", actualMessage, StringComparison.InvariantCulture);
            Assert.Contains(@"""RX2Freq"":869500000,", actualMessage, StringComparison.InvariantCulture);

            Assert.Contains(@"""priority"":0", actualMessage, StringComparison.InvariantCulture);
            Assert.DoesNotContain("RxDelay", actualMessage, StringComparison.InvariantCulture);
            Assert.DoesNotContain("RX1DR", actualMessage, StringComparison.InvariantCulture);
            Assert.DoesNotContain("RX1Freq", actualMessage, StringComparison.InvariantCulture);
            Assert.DoesNotContain(@"""xtime"":123456", actualMessage, StringComparison.InvariantCulture);

            if (rfchHasValue)
            {
                Assert.Contains(@"""rctx"":1,", actualMessage, StringComparison.InvariantCulture);
            }
            else
            {
                Assert.DoesNotContain("rctx", actualMessage, StringComparison.InvariantCulture);
            }
        }

        [Fact]
        public async Task SendDownstreamAsync_Fails_WithNullMessage()
        {
            // act and assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => this.downlinkSender.SendDownstreamAsync(null));
        }

        [Fact]
        public async Task SendDownstreamAsync_Fails_WithNonNullMessage_ButDefaultStationEui()
        {
            // arrange
            var downlinkMessage = new DownlinkMessage(this.loraDataByteArray,
                                                      0,
                                                      (DataRateIndex.DR5, Hertz.Mega(868.5)),
                                                      (DataRateIndex.DR0, Hertz.Mega(868.5)),
                                                      this.devEui,
                                                      RxDelay0,
                                                      LoRaDeviceClassType.C);

            // act and assert
            await Assert.ThrowsAsync<ArgumentException>(() => this.downlinkSender.SendDownstreamAsync(downlinkMessage));
        }
    }
}
