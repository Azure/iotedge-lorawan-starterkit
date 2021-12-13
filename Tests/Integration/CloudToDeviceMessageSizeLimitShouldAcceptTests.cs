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
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Moq;
    using Xunit;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Cloud to device message processing max payload size tests (Join tests are handled in other class)
    [Collection(TestConstants.C2D_Size_Limit_TestCollectionName)]
    public class CloudToDeviceMessageSizeLimitShouldAcceptTests : CloudToDeviceMessageSizeLimitBaseTests
    {
        [Theory(Skip = "Fails on CI - works locally. To enable with #562")]
        [CombinatorialData]
        public async Task Should_Accept(
            bool isConfirmed,
            bool hasMacInUpstream,
            bool hasMacInC2D,
            bool isTooLongForUpstreamMacCommandInAnswer,
            bool isSendingInRx2,
            [CombinatorialValues("SF10BW125", "SF9BW125", "SF8BW125", "SF7BW125")] string datr)
        {
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            // This scenario makes no sense
            if (hasMacInUpstream && isTooLongForUpstreamMacCommandInAnswer)
                return;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            var rxpk = CreateUpstreamRxpk(isConfirmed, hasMacInUpstream, datr, simulatedDevice);

            if (!hasMacInUpstream)
            {
                LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                    .ReturnsAsync(true);
            }

            var euRegion = RegionManager.EU868;
            var c2dMessageMacCommand = new DevStatusRequest();
            var c2dMessageMacCommandSize = hasMacInC2D ? c2dMessageMacCommand.Length : 0;
            var upstreamMessageMacCommandSize = 0;
            string expectedDownlinkDatr;

            if (hasMacInUpstream && !isTooLongForUpstreamMacCommandInAnswer)
            {
                upstreamMessageMacCommandSize = new LinkCheckAnswer(1, 1).Length;
            }

            expectedDownlinkDatr = isSendingInRx2
                ? euRegion.DRtoConfiguration[euRegion.GetDefaultRX2ReceiveWindow().DataRate].configuration
                : datr;

#pragma warning disable CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
            var c2dPayloadSize = euRegion.GetMaxPayloadSize(expectedDownlinkDatr)
                - c2dMessageMacCommandSize
                - upstreamMessageMacCommandSize
                - Constants.LoraProtocolOverheadSize;
#pragma warning restore CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done

            var c2dMessagePayload = TestUtils.GeneratePayload("123457890", (int)c2dPayloadSize);

            var c2dMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = c2dMessagePayload,
                Fport = 1,
            };

            if (hasMacInC2D)
            {
                c2dMessage.MacCommands.ResetTo(new[] { c2dMessageMacCommand });
            }

            using var cloudToDeviceMessage = c2dMessage.CreateMessage();

            LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null);

            LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            using var cache = EmptyMemoryCache();
            using var loraDeviceCache = CreateDeviceCache(loraDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            using var messageProcessor = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            var startTimeOffset = isSendingInRx2 ? TestUtils.GetStartTimeOffsetForSecondWindow() : TimeSpan.Zero;

            using var request = CreateWaitableRequest(rxpk, startTimeOffset: startTimeOffset);
            messageProcessor.DispatchRequest(request);

            // Expectations
            // 1. Message was sent to IoT Hub
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.Equal(expectedDownlinkDatr, request.ResponseDownlink.Txpk.Datr);

            // Get downlink message
            var downlinkMessage = PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loraDevice.AppSKey);

            // 3. downlink message payload contains expected message type and DevAddr
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            // 4. Expected Mac commands are present
            var expectedMacCommandsCount = 0;

            if (hasMacInC2D)
                expectedMacCommandsCount++;
            if (hasMacInUpstream && !isTooLongForUpstreamMacCommandInAnswer)
                expectedMacCommandsCount++;

            if (expectedMacCommandsCount > 0)
            {
                // Possible problem: Manually casting payloadDataDown.Fopts to array and reversing it
                var macCommands = MacCommand.CreateServerMacCommandFromBytes(simulatedDevice.DevEUI, payloadDataDown.Fopts.ToArray().Reverse().ToArray());
                Assert.Equal(expectedMacCommandsCount, macCommands.Count);
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
