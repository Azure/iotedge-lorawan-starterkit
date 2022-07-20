// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    [Collection(TestConstants.C2D_Size_Limit_TestCollectionName)]
    public class CloudToDeviceMessageSizeLimitShouldAbandonTests : CloudToDeviceMessageSizeLimitBaseTests
    {
        public CloudToDeviceMessageSizeLimitShouldAbandonTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

        [Theory]
        [CombinatorialData]
        public async Task Should_Abandon(
            bool isConfirmed,
            bool hasMacInUpstream,
            bool hasMacInC2D,
            [CombinatorialValues("SF9BW125", "SF8BW125", "SF7BW125")] string datr)
        {
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
            TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
            frmCntUp: InitialDeviceFcntUp,
            frmCntDown: InitialDeviceFcntDown);

            var loraDevice = CreateLoRaDevice(simulatedDevice);
            var msgPayload = "1234567890";
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage(msgPayload, isHexPayload: true, fport: 0);

            var (radioMetaData, loraPayload) = CreateUpstreamMessage(isConfirmed, hasMacInUpstream, LoRaDataRate.Parse(datr), simulatedDevice);

            if (!hasMacInUpstream)
            {
                LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                    .ReturnsAsync(true);
            }

            var euRegion = RegionManager.EU868;
            var c2dMessageMacCommand = new DevStatusRequest();
            var c2dMessageMacCommandSize = hasMacInC2D ? c2dMessageMacCommand.Length : 0;
            DataRateIndex expectedDownlinkDatr;

            expectedDownlinkDatr = euRegion.GetDataRateIndex(euRegion.DRtoConfiguration[euRegion.GetDefaultRX2ReceiveWindow(default).DataRate].DataRate);

            var c2dPayloadSize = euRegion.GetMaxPayloadSize(euRegion.GetDefaultRX2ReceiveWindow(default).DataRate)
                - c2dMessageMacCommandSize
                + 1 // make message too long on purpose
                - NetworkServer.Constants.LoraProtocolOverheadSize;

            var c2dMessagePayload = TestUtils.GeneratePayload("123457890", (int)c2dPayloadSize);

            var c2dMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = c2dMessagePayload,
                Fport = FramePorts.App1,
            };

            if (hasMacInC2D)
            {
                c2dMessage.MacCommands.Add(c2dMessageMacCommand);
            }

            using var cloudToDeviceMessage = c2dMessage.CreateMessage();

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .Callback<TimeSpan>((_) =>
                {
                    LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                        .ReturnsAsync((Message)null);
                })
                .ReturnsAsync(cloudToDeviceMessage);

            LoRaDeviceClient.Setup(x => x.AbandonAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(loraDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(radioMetaData, loraPayload, startTimeOffset: TestUtils.GetStartTimeOffsetForSecondWindow(), constantElapsedTime: TimeSpan.FromMilliseconds(1002));
            messageProcessor.DispatchRequest(request);

            // Expectations
            // 1. Message was sent to IoT Hub
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);

            Assert.Equal(expectedDownlinkDatr, request.ResponseDownlink.Rx2.DataRate);

            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
            if (hasMacInUpstream)
            {
                payloadDataDown.Serialize(loraDevice.NwkSKey ?? throw new InvalidOperationException("NwkSKey can't be null"));
            } else
            {
                payloadDataDown.Serialize(loraDevice.AppSKey ?? throw new InvalidOperationException("AppSKey can't be null"));
            }

            // 3. Fpending flag is set
            Assert.True(payloadDataDown.IsDownlinkFramePending);

            Assert.Equal(payloadDataDown.DevAddr, loraDevice.DevAddr);
            Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.MessageType);

            // Expected Mac command is present
            if (hasMacInUpstream)
            {
                // Possible problem: manually casting frmPayload to array. No reversal.
                var frmPayload = payloadDataDown.Frmpayload.ToArray();
                var macCommands = MacCommand.CreateServerMacCommandFromBytes(frmPayload);
                Assert.Single(macCommands);
                Assert.IsType<LinkCheckAnswer>(macCommands.First());
            }
            else
            {
                Assert.Null(payloadDataDown.MacCommands);
            }

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }
    }
}
