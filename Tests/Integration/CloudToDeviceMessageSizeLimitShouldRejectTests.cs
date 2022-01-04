// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Moq;
    using Xunit;

    [Collection(TestConstants.C2D_Size_Limit_TestCollectionName)]
    public class CloudToDeviceMessageSizeLimitShouldRejectTests : CloudToDeviceMessageSizeLimitBaseTests
    {
        [Theory]
        [CombinatorialData]
        public async Task Should_Reject(
            bool isConfirmed,
            bool hasMacInUpstream,
            bool hasMacInC2D,
            [CombinatorialValues("SF10BW125", "SF9BW125", "SF8BW125", "SF7BW125")] string datr)
        {
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            var (radioMetaData, loraPayload) = CreateUpstreamMessage(isConfirmed, hasMacInUpstream, LoRaDataRate.Parse(datr), simulatedDevice);

            if (!hasMacInUpstream)
            {
                LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                    .ReturnsAsync(true);
            }

            var region = TestUtils.TestRegion;
            var c2dMessageMacCommand = new DevStatusRequest();
            var c2dMessageMacCommandSize = hasMacInC2D ? c2dMessageMacCommand.Length : 0;
            var expectedDownlinkDatr = region.GetDataRateIndex(LoRaDataRate.Parse(datr));

            var c2dPayloadSize = region.GetMaxPayloadSize(expectedDownlinkDatr)
                - c2dMessageMacCommandSize
                + 1 // make message too long on purpose
                - Constants.LoraProtocolOverheadSize;

            var c2dMessagePayload = TestUtils.GeneratePayload("123457890", (int)c2dPayloadSize);

            var c2dMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = c2dMessagePayload,
                Fport = FramePorts.App1,
            };

            if (hasMacInC2D)
            {
                c2dMessage.MacCommands.ResetTo(new[] { c2dMessageMacCommand });
            }

            using var cloudToDeviceMessage = c2dMessage.CreateMessage();

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage);

            LoRaDeviceClient.Setup(x => x.RejectAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            using var cache = EmptyMemoryCache();
            using var loraDeviceCache = CreateDeviceCache(loraDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var request = CreateWaitableRequest(radioMetaData, loraPayload, constantElapsedTime: TimeSpan.Zero);
            messageProcessor.DispatchRequest(request);

            // Expectations
            // 1. Message was sent to IoT Hub
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            var shouldHaveADownlink = isConfirmed || hasMacInUpstream;

            // 2. Return is downstream message ONLY
            // if is confirmed or had Mac commands in upstream message
            if (shouldHaveADownlink)
            {
                Assert.NotNull(request.ResponseDownlink);

                // TODO CHANGE THIS WHEN MOVING RXPK in #1086
                // Assert.Equal(expectedDownlinkDatr, request.ResponseDownlink.Txpk.Datr);

                var downlinkMessage = PacketForwarder.DownlinkMessages[0];
                var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
                payloadDataDown.PerformEncryption(loraDevice.AppSKey.Value);

                Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
                Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.MessageType);

                if (hasMacInUpstream)
                {
                    Assert.Equal(new LinkCheckAnswer(1, 1).Length, payloadDataDown.Frmpayload.Length);
                    Assert.Equal(FramePort.MacCommand, payloadDataDown.Fport);
                }
            }
            else
            {
                Assert.Null(request.ResponseDownlink);
            }

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }
    }
}
