// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Cloud to device message processing max payload size tests (Join tests are handled in other class)
    [Collection(TestConstants.C2D_Size_Limit_TestCollectionName)]
    public class MessageProcessor_End2End_NoDep_CloudToDeviceMessage_SizeLimit_Should_Accept : MessageProcessor_End2End_NoDep_CloudToDeviceMessage_SizeLimitBase
    {
        [Theory]
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
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            Rxpk rxpk = this.CreateUpstreamRxpk(isConfirmed, hasMacInUpstream, datr, simulatedDevice);

            if (!hasMacInUpstream)
            {
                this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
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

            if (isSendingInRx2)
                expectedDownlinkDatr = euRegion.DRtoConfiguration[euRegion.RX2DefaultReceiveWindows.dr].configuration;
            else
                expectedDownlinkDatr = datr;

            var c2dPayloadSize = euRegion.GetMaxPayloadSize(expectedDownlinkDatr)
                - c2dMessageMacCommandSize
                - upstreamMessageMacCommandSize
                - Constants.LORA_PROTOCOL_OVERHEAD_SIZE;

            var c2dMessagePayload = TestUtils.GeneratePayload("123457890", (int)c2dPayloadSize);

            var c2dMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = c2dMessagePayload,
                Fport = 1,
            };

            if (hasMacInC2D)
            {
                c2dMessage.MacCommands = new[] { c2dMessageMacCommand };
            }

            var cloudToDeviceMessage = c2dMessage.CreateMessage();

            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync(null);

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var startTimeOffset = isSendingInRx2 ? TestUtils.GetStartTimeOffsetForSecondWindow() : TimeSpan.Zero;

            var request = this.CreateWaitableRequest(rxpk, startTimeOffset: startTimeOffset);
            messageProcessor.DispatchRequest(request);

            // Expectations
            // 1. Message was sent to IoT Hub
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.Equal(expectedDownlinkDatr, request.ResponseDownlink.Txpk.Datr);

            // Get downlink message
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
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

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }
    }
}