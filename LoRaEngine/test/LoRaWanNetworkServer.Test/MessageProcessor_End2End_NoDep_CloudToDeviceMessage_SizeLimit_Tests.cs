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
    // Cloud to device message processing tests (Join tests are handled in other class)
    public class MessageProcessor_End2End_NoDep_CloudToDeviceMessage_SizeLimit_Tests : MessageProcessorTestBase
    {
        public MessageProcessor_End2End_NoDep_CloudToDeviceMessage_SizeLimit_Tests()
        {
        }

        [Theory]
        [InlineData("SF12BW125", "123456789012345678901234567890123456789012345678901")] // 51
        [InlineData("SF11BW125", "123456789012345678901234567890123456789012345678901")] // 51
        [InlineData("SF10BW125", "123456789012345678901234567890123456789012345678901")] // 51
        [InlineData("SF9BW125", "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345")] // 115
        [InlineData("SF8BW125", "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012")] // 222
        [InlineData("SF7BW125", "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012")] // 222
        [InlineData("SF7BW250", "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012")] // 222

        public async Task OTAA_Confirmed_With_Cloud_To_Device_Message_Returns_Downstream_Message_Size_OK(string customDatr, string c2dMessageContent)
        {
            const int PayloadFcnt = 10;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var needsToSaveFcnt = PayloadFcnt - InitialDeviceFcntUp >= Constants.MAX_FCNT_UNSAVED_DELTA;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            if (needsToSaveFcnt)
            {
                this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                    .ReturnsAsync(true);
            }

            var cloudToDeviceMessage = new LoRaCloudToDeviceMessage() { Payload = c2dMessageContent, Fport = 1 }
                .CreateMessage();

            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            rxpk.Datr = customDatr;
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loraDevice.AppSKey);
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed());
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            // 3. Payload should not be empty
            Assert.False(payloadDataDown.Frmpayload.IsEmpty);

            // 4. Expected payload is present
            Assert.Equal(
                Encoding.UTF8.GetString(payloadDataDown.Frmpayload.ToArray()),
                c2dMessageContent);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.GetFcnt());

            // 6. Frame count has pending changes?
            if (needsToSaveFcnt)
                Assert.False(loraDevice.HasFrameCountChanges);
            else
                Assert.True(loraDevice.HasFrameCountChanges);
        }

        [Theory]
        [InlineData("SF12BW125", "1234567890123456789012345678901234567890123456789012")] // 52
        [InlineData("SF11BW125", "1234567890123456789012345678901234567890123456789012")] // 52
        [InlineData("SF10BW125", "1234567890123456789012345678901234567890123456789012")] // 52
        [InlineData("SF9BW125", "12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456")] // 116
        [InlineData("SF8BW125", "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123")] // 223
        [InlineData("SF7BW125", "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123")] // 223
        [InlineData("SF7BW250", "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123")] // 223

        public async Task OTAA_Confirmed_With_Cloud_To_Device_Message_Returns_Downstream_Message_Size_Too_Long(string customDatr, string c2dMessageContent)
        {
            const int PayloadFcnt = 10;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var needsToSaveFcnt = PayloadFcnt - InitialDeviceFcntUp >= Constants.MAX_FCNT_UNSAVED_DELTA;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            if (needsToSaveFcnt)
            {
                this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                    .ReturnsAsync(true);
            }

            var cloudToDeviceMessage = new LoRaCloudToDeviceMessage() { Payload = c2dMessageContent, Fport = 1 }
                .CreateMessage();

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

            var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            rxpk.Datr = customDatr;
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loraDevice.AppSKey);

            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed());
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            // Payload should be empty
            Assert.True(payloadDataDown.Frmpayload.IsEmpty);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.GetFcnt());

            // 6. Frame count has pending changes?
            if (needsToSaveFcnt)
                Assert.False(loraDevice.HasFrameCountChanges);
            else
                Assert.True(loraDevice.HasFrameCountChanges);
        }

        [Theory]
        [InlineData("SF12BW125", "12345678901234567890123456789012345678901234567890")] // 51 - 1 (Mac Command)
        [InlineData("SF11BW125", "12345678901234567890123456789012345678901234567890")] // 51 - 1 (Mac Command)
        [InlineData("SF10BW125", "12345678901234567890123456789012345678901234567890")] // 51 - 1 (Mac Command)
        [InlineData("SF9BW125", "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234")] // 115 - 1 (Mac Command)
        [InlineData("SF8BW125", "12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901")] // 222 - 1 (Mac Command)
        [InlineData("SF7BW125", "12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901")] // 222 - 1 (Mac Command)
        [InlineData("SF7BW250", "12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901")] // 222 - 1 (Mac Command)

        public async Task OTAA_Confirmed_With_Cloud_To_Device_Mac_Command_Returns_Downstream_Message_Size_OK(string customDatr, string c2dMessageContent)
        {
            const int PayloadFcnt = 20;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var c2d = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageContent,
                MacCommands = new[]
                {
                    new DevStatusRequest(),
                },
                Fport = 1,
            };

            var cloudToDeviceMessage = c2d.CreateMessage();

            this.LoRaDeviceClient.SetupSequence(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage)
                .ReturnsAsync((Message)null); // 2nd cloud to device message does not return anything

            this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            rxpk.Datr = customDatr;
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loraDevice.AppSKey);

            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed());
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            // 3. Payload should not be empty
            Assert.False(payloadDataDown.Frmpayload.IsEmpty);

            // 4. Expected payload is present
            Assert.Equal(
                Encoding.UTF8.GetString(payloadDataDown.Frmpayload.ToArray()),
                c2dMessageContent);

            // 5. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            // 6. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.GetFcnt());

            // 7. Frame count has no pending changes
            Assert.False(loraDevice.HasFrameCountChanges);

            // 8. Mac commands should be present in reply
            // mac command is in fopts if there is a c2d message
            Assert.NotNull(payloadDataDown.Fopts.Span.ToArray());
            Assert.Single(payloadDataDown.Fopts.Span.ToArray());
            Assert.Equal((byte)LoRaTools.CidEnum.DevStatusCmd, payloadDataDown.Fopts.Span[0]);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData("SF12BW125", "123456789012345678901234567890123456789012345678901")] // > 51 - 1 (Mac Command)
        [InlineData("SF11BW125", "123456789012345678901234567890123456789012345678901")] // > 51 - 1 (Mac Command)
        [InlineData("SF10BW125", "123456789012345678901234567890123456789012345678901")] // > 51 - 1 (Mac Command)
        [InlineData("SF9BW125", "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345")] // > 115 - 1 (Mac Command)
        [InlineData("SF8BW125", "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012")] // > 222 - 1 (Mac Command)
        [InlineData("SF7BW125", "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012")] // > 222 - 1 (Mac Command)
        [InlineData("SF7BW250", "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012")] // > 222 - 1 (Mac Command)

        public async Task OTAA_Confirmed_With_Cloud_To_Device_Mac_Command_Returns_Downstream_Message_Size_Too_Long(string customDatr, string c2dMessageContent)
        {
            const int PayloadFcnt = 20;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var c2d = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageContent,
                MacCommands = new[]
                {
                    new DevStatusRequest(),
                },
                Fport = 1,
            };

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

            var payload = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: PayloadFcnt);
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            rxpk.Datr = customDatr;
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            // 2. Return is downstream message
            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            payloadDataDown.PerformEncryption(loraDevice.AppSKey);

            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed());
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);

            // 3. Payload should be empty
            Assert.True(payloadDataDown.Frmpayload.IsEmpty);

            // 4. Frame counter up was updated
            Assert.Equal(PayloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.GetFcnt());

            // 6. Frame count has no pending changes
            Assert.False(loraDevice.HasFrameCountChanges);

            // 7. Mac commands should NOT be present in reply
            // mac command is in fopts if there is a c2d message
            Assert.True(payloadDataDown.Fopts.Span.IsEmpty);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [CombinatorialData]
        public async Task Aaaaaaaaaaaaaaaargh(
            bool isConfirmed,
            bool hasMacInUpstream,
            bool hasMacInC2D,
            MessagePayloadSize msgPayloadSize,
            bool isTooLongForUpstreamMacCommandInAnswer, // true: drop the upstream mac answer
            bool isSendingInRx2,
            [CombinatorialValues("SF12BW125", "SF11BW125", "SF10BW125", "SF9BW125", "SF8BW125", "SF7BW125", "SF7BW250")] string datr)
        {
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 20;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

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
                    var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("1234");
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

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var euRegion = RegionFactory.CreateEU868Region();
            var c2dMessageMacCommand = new DevStatusRequest();
            var c2dMessageMacCommandSize = hasMacInC2D ? c2dMessageMacCommand.Length : 0;
            var upstreamMessageMacCommandSize = 0;
            string expectedDownlinkDatr;
            bool isTooLong = false;

            if (hasMacInUpstream && !isTooLongForUpstreamMacCommandInAnswer)
                upstreamMessageMacCommandSize = new LinkCheckAnswer(1, 1).Length;

            if (isSendingInRx2)
            {
                expectedDownlinkDatr = euRegion.DRtoConfiguration[euRegion.RX2DefaultReceiveWindows.dr].configuration;
                if (msgPayloadSize == MessagePayloadSize.TooLongForRx1 || msgPayloadSize == MessagePayloadSize.TooLongForRx2)
                    isTooLong = true;
            }
            else
            {
                expectedDownlinkDatr = datr;
                if (msgPayloadSize == MessagePayloadSize.TooLongForRx1)
                    isTooLong = true;
            }

            var c2dPayloadSize = euRegion.GetMaxPayloadSize(expectedDownlinkDatr)
                - upstreamMessageMacCommandSize
                - c2dMessageMacCommandSize
                + (isTooLong ? 1 : 0);

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
                    .ReturnsAsync(cloudToDeviceMessage, TimeSpan.FromSeconds(1));
            }
            else
            {
                this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                    .ReturnsAsync(cloudToDeviceMessage);
            }

            var shouldCompleteMessage = !isTooLong;
            var shouldAbandonMessage = isSendingInRx2 && msgPayloadSize == MessagePayloadSize.TooLongForRx2;

            if (shouldCompleteMessage)
            {
                this.LoRaDeviceClient.Setup(x => x.CompleteAsync(cloudToDeviceMessage))
                    .ReturnsAsync(true);
            }
            else if (shouldAbandonMessage)
            {
                this.LoRaDeviceClient.Setup(x => x.AbandonAsync(cloudToDeviceMessage))
                    .ReturnsAsync(true);
            }
            else
            {
                this.LoRaDeviceClient.Setup(x => x.RejectAsync(cloudToDeviceMessage))
                    .ReturnsAsync(true);
            }

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

            var shouldHaveADownlink = isConfirmed || !isTooLong || hasMacInUpstream;

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

                // Confirmed or unconfirmed message
                Assert.Equal(isConfirmed, payloadDataDown.IsConfirmed());

                // Message too long, Payload should be empty
                if (isTooLong)
                {
                    Assert.True(payloadDataDown.Frmpayload.IsEmpty);
                }

                // Message not too long, expected payload should be present
                else
                {
                    Assert.Equal(
                        Encoding.UTF8.GetString(payloadDataDown.Frmpayload.ToArray()),
                        c2dMsgPayload);
                }

                // Expected Mac commands are present
                var expectedMacCommandsCount = 0;

                if (!isTooLong && hasMacInC2D)
                    expectedMacCommandsCount++;
                if (hasMacInUpstream && !isTooLongForUpstreamMacCommandInAnswer)
                    expectedMacCommandsCount++;

                Assert.Equal(expectedMacCommandsCount, payloadDataDown.MacCommands.Count);
            }
            else
            {
                Assert.Null(request.ResponseDownlink);
            }

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
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

        public enum MessagePayloadSize
        {
            Ok,
            TooLongForRx1,
            TooLongForRx2
        }
    }
}