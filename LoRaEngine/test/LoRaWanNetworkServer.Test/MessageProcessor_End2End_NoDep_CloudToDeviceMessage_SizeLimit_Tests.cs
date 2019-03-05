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
    public class MessageProcessor_End2End_NoDep_CloudToDeviceMessage_SizeLimit_Tests : MessageProcessorTestBase
    {
        public MessageProcessor_End2End_NoDep_CloudToDeviceMessage_SizeLimit_Tests()
        {
        }

        [Theory]
        [CombinatorialData]
        public async Task MessageProcessor_End2End_NoDep_CloudToDeviceMessage_SizeLimit_Should_Reject(
            bool isConfirmed,
            bool hasMacInUpstream,
            bool hasMacInC2D,
            [CombinatorialValues("SF10BW125", "SF9BW125", "SF8BW125", "SF7BW125")] string datr)
        {
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            Rxpk rxpk = CreateUpstreamRxpk(isConfirmed, hasMacInUpstream, datr, simulatedDevice);

            if (!hasMacInUpstream)
            {
                this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                    .ReturnsAsync(true);
            }

            var euRegion = RegionFactory.CreateEU868Region();
            var c2dMessageMacCommand = new DevStatusRequest();
            var c2dMessageMacCommandSize = hasMacInC2D ? c2dMessageMacCommand.Length : 0;
            var upstreamMessageMacCommandSize = 0;
            string expectedDownlinkDatr;

            if (hasMacInUpstream)
            {
                upstreamMessageMacCommandSize = new LinkCheckAnswer(1, 1).Length;
            }

            expectedDownlinkDatr = datr;

            var c2dPayloadSize = euRegion.GetMaxPayloadSize(expectedDownlinkDatr)
                - c2dMessageMacCommandSize
                + 1 // make message too long on purpose
                - Constants.LORA_PROTOCOL_OVERHEAD_SIZE;

            var c2dMsgPayload = this.GeneratePayload("123457890", (int)c2dPayloadSize);

            var c2d = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMsgPayload,
                Fport = 1,
            };

            if (hasMacInC2D)
            {
                c2d.MacCommands = new[] { c2dMessageMacCommand };
            }

            var cloudToDeviceMessage = c2d.CreateMessage();

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage);

            this.LoRaDeviceClient.Setup(x => x.RejectAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);

            // Expectations
            // 1. Message was sent to IoT Hub
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            var shouldHaveADownlink = isConfirmed || hasMacInUpstream;

            if (shouldHaveADownlink)
            {
                // 2. Return is downstream message
                Assert.NotNull(request.ResponseDownlink);
                Assert.Equal(expectedDownlinkDatr, request.ResponseDownlink.Txpk.Datr);

                var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
                var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
                payloadDataDown.PerformEncryption(loraDevice.AppSKey);

                Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
                Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

                if (hasMacInUpstream)
                {
                    Assert.Equal(new LinkCheckAnswer(1, 1).Length, payloadDataDown.Frmpayload.Length);
                    Assert.Equal(0, payloadDataDown.GetFPort());
                }
            }
            else
            {
                Assert.Null(request.ResponseDownlink);
            }

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [CombinatorialData]
        public async Task MessageProcessor_End2End_NoDep_CloudToDeviceMessage_SizeLimit_Should_Accept(
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

            Rxpk rxpk = CreateUpstreamRxpk(isConfirmed, hasMacInUpstream, datr, simulatedDevice);

            if (!hasMacInUpstream)
            {
                this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                    .ReturnsAsync(true);
            }

            var euRegion = RegionFactory.CreateEU868Region();
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

            var c2dMsgPayload = this.GeneratePayload("123457890", (int)c2dPayloadSize);

            var c2d = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMsgPayload,
                Fport = 1,
            };

            if (hasMacInC2D)
            {
                c2d.MacCommands = new[] { c2dMessageMacCommand };
            }

            var cloudToDeviceMessage = c2d.CreateMessage();

            if (isSendingInRx2)
            {
                this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                    .Callback<TimeSpan>((_) =>
                    {
                        this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                            .ReturnsAsync((Message)null);
                    })
                    .ReturnsAsync(cloudToDeviceMessage, TimeSpan.FromSeconds(1));
            }
            else
            {
                this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                    .ReturnsAsync(cloudToDeviceMessage)
                    .ReturnsAsync(null);
            }

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);

            // Expectations
            // 1. Message was sent to IoT Hub
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.Equal(expectedDownlinkDatr, request.ResponseDownlink.Txpk.Datr);

            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loraDevice.AppSKey);

            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            // Expected Mac commands are present
            var expectedMacCommandsCount = 0;

            if (hasMacInC2D)
                expectedMacCommandsCount++;
            if (hasMacInUpstream && !isTooLongForUpstreamMacCommandInAnswer)
                expectedMacCommandsCount++;

            if (expectedMacCommandsCount > 0)
            {
                var macCommands = MacCommand.CreateServerMacCommandFromBytes(simulatedDevice.DevEUI, payloadDataDown.Fopts);
                Assert.Equal(expectedMacCommandsCount, macCommands.Count);
            }
            else
            {
                Assert.Null(payloadDataDown.MacCommands);
            }

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [CombinatorialData]
        public async Task MessageProcessor_End2End_NoDep_CloudToDeviceMessage_SizeLimit_Should_Abandon(
            bool isConfirmed,
            bool hasMacInUpstream,
            bool hasMacInC2D,
            [CombinatorialValues("SF9BW125", "SF8BW125", "SF7BW125")] string datr)
        {
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
            TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
            frmCntUp: InitialDeviceFcntUp,
            frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            Rxpk rxpk = CreateUpstreamRxpk(isConfirmed, hasMacInUpstream, datr, simulatedDevice);

            if (!hasMacInUpstream)
            {
                this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                    .ReturnsAsync(true);
            }

            var euRegion = RegionFactory.CreateEU868Region();
            var c2dMessageMacCommand = new DevStatusRequest();
            var c2dMessageMacCommandSize = hasMacInC2D ? c2dMessageMacCommand.Length : 0;
            var upstreamMessageMacCommandSize = 0;
            string expectedDownlinkDatr;

            if (hasMacInUpstream)
            {
                upstreamMessageMacCommandSize = new LinkCheckAnswer(1, 1).Length;
            }

            expectedDownlinkDatr = euRegion.DRtoConfiguration[euRegion.RX2DefaultReceiveWindows.dr].configuration;

            var c2dPayloadSize = euRegion.GetMaxPayloadSize(expectedDownlinkDatr)
                - c2dMessageMacCommandSize
                + 1 // make message too long on purpose
                - Constants.LORA_PROTOCOL_OVERHEAD_SIZE;

            var c2dMsgPayload = this.GeneratePayload("123457890", (int)c2dPayloadSize);

            var c2d = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMsgPayload,
                Fport = 1,
            };

            if (hasMacInC2D)
            {
                c2d.MacCommands = new[] { c2dMessageMacCommand };
            }

            var cloudToDeviceMessage = c2d.CreateMessage();

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .Callback<TimeSpan>((_) =>
                {
                    this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                        .ReturnsAsync((Message)null);
                })
                .ReturnsAsync(cloudToDeviceMessage, TimeSpan.FromSeconds(1));

            this.LoRaDeviceClient.Setup(x => x.AbandonAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);

            // Expectations
            // 1. Message was sent to IoT Hub
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.Equal(expectedDownlinkDatr, request.ResponseDownlink.Txpk.Datr);

            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loraDevice.AppSKey);

            Assert.Equal((byte)FctrlEnum.FpendingOrClassB, payloadDataDown.Fctrl.Span[0] & (byte)FctrlEnum.FpendingOrClassB);

            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            // Expected Mac command is present
            if (hasMacInUpstream)
            {
                var frmPayload = payloadDataDown.Frmpayload.ToArray().Reverse().ToArray();
                var macCommands = MacCommand.CreateServerMacCommandFromBytes(simulatedDevice.DevEUI, frmPayload);
                Assert.Single(macCommands);
                Assert.IsType<LinkCheckAnswer>(macCommands.First());
            }
            else
            {
                Assert.Null(payloadDataDown.MacCommands);
            }

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        private static Rxpk CreateUpstreamRxpk(bool isConfirmed, bool hasMacInUpstream, string datr, SimulatedDevice simulatedDevice)
        {
            Rxpk rxpk = null;
            string msgPayload = null;

            if (isConfirmed)
            {
                if (hasMacInUpstream)
                {
                    // Cofirmed message with Mac command in upstream
                    msgPayload = "02";
                    var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage(msgPayload, isHexPayload: true, fport: 0);
                    rxpk = confirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, datr: datr).Rxpk[0];
                }
                else
                {
                    // Cofirmed message without Mac command in upstream
                    msgPayload = "1234567890";
                    var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage(msgPayload);
                    rxpk = confirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, datr: datr).Rxpk[0];
                }
            }
            else
            {
                if (hasMacInUpstream)
                {
                    // Uncofirmed message with Mac command in upstream
                    msgPayload = "02";
                    var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, isHexPayload: true, fport: 0);
                    rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, datr: datr).Rxpk[0];
                }
                else
                {
                    // Uncofirmed message without Mac command in upstream
                    msgPayload = "1234567890";
                    var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload);
                    rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, datr: datr).Rxpk[0];
                }
            }

            return rxpk;
        }

        private string GeneratePayload(string allowedChars, int length)
        {
            Random random = new Random();

            char[] chars = new char[length];
            int setLength = allowedChars.Length;

            for (int i = 0; i < length; ++i)
            {
                chars[i] = allowedChars[random.Next(setLength)];
            }

            return new string(chars, 0, length);
        }
    }
}