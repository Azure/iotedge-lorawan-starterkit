// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanNetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.Test;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    public class ADRTest : MessageProcessorTestBase
    {
        [Theory]
        // deviceId, # messages sent, isAdrReplyExpected, ExpectedDR, expectedPower, expectedNbRep
        [InlineData(10, 70, true, 5, 7, 1)]
        [InlineData(11, 15, false, 0, 0, 0)]
        public async Task Perform_Rate_Adapatation_When_Possible(uint deviceId, int count, bool expectADRAdaptation, int expectedDR, int expectedTXPower, int expectedNbRep)
        {
            int payloadFcnt = 0;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 0;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(deviceId, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // todo add case without buffer
            for (int i = 0; i < count; i++)
            {
                var payloadInt = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrl: (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
                var rxpkInt = payloadInt.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
                var requestInt = this.CreateWaitableRequest(rxpkInt);
                messageProcessor.DispatchRequest(requestInt);
                Assert.True(await requestInt.WaitCompleteAsync(-1));
                payloadFcnt++;
            }

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrl: (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADRAckReq + (int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();

            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));

            if (expectADRAdaptation)
            {
                // We expect a mac command in the payload
                Assert.Equal(5, payloadDataDown.Frmpayload.Span.Length);
                var decryptedPayload = payloadDataDown.PerformEncryption(simulatedDevice.NwkSKey);
                Assert.Equal(0, payloadDataDown.Fport.Span[0]);
                Assert.Equal((byte)CidEnum.LinkADRCmd, decryptedPayload[0]);
                var linkAdr = new LinkADRRequest(decryptedPayload);
                Assert.Equal(expectedDR, linkAdr.DataRate);
                Assert.Equal(expectedDR, loraDevice.DataRate);
                Assert.Equal(expectedTXPower, linkAdr.TxPower);
                Assert.Equal(expectedTXPower, loraDevice.TxPower);
                Assert.Equal(expectedNbRep, linkAdr.NbRep);
                Assert.Equal(expectedNbRep, loraDevice.NbRep);

                // in case no payload the mac is in the FRMPayload and is decrypted with NwkSKey
                Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
                Assert.False(payloadDataDown.IsConfirmed);
                Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);
                // 4. Frame counter up was updated
                Assert.Equal(payloadFcnt, loraDevice.FCntUp);

                // 5. Frame counter down is updated
                Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
                Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.GetFcnt());

                // 6. Frame count has no pending changes
                Assert.False(loraDevice.HasFrameCountChanges);
            }
            else
            {
                // In this case, no ADR adaptation is performed, so the message should be empty
                Assert.Equal(0, payloadDataDown.Frmpayload.Span.Length);
                Assert.Equal(0, payloadDataDown.Fopts.Span.Length);
            }
        }

        [Theory]
        // deviceId, lsnr, inputDR, outputDR
        // DR5 with poor lsnr can't degrade DR
        [InlineData(1, -20, "SF7BW125", 5, 0)]
        // DR0 with high lsnr will result in move to DR5
        [InlineData(2, 20, "SF12BW125", 5, 7)]
        // DR1 with high lsnr will result in move to DR5
        [InlineData(3, 20, "SF11BW125", 5, 7)]
        // DR3 with high lsnr will result in move to DR5
        [InlineData(4, 20, "SF9BW125", 5, 7)]
        // DR5 with high lsnr will result in reduce the TxPower to 7
        [InlineData(5, 20, "SF9BW125", 5, 7)]
        // DR5 with low lsnr will not try to modify dr and already at maxTxPower
        [InlineData(6, -10, "SF9BW125", 5, 0)]
        // Device 5 massive increase in txpower due to very bad lsnr
        [InlineData(5, -30, "SF9BW125", 3, 0)]
        public async Task Perform_DR_Adaptation_When_Needed(uint deviceId, float currentLsnr, string currentDR, int expectedDR, int expectedTxPower)
        {
            int messageCount = 20;
            int payloadFcnt = 0;
            const int InitialDeviceFcntUp = 9;
            const int ExpectedDeviceFcntDown = 0;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(deviceId, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
            .Callback<TwinCollection>((t) =>
            {
                if (t.Contains(TwinProperty.DataRate))
                    Assert.Equal(expectedDR, (int)t[TwinProperty.DataRate]);
                if (t.Contains(TwinProperty.TxPower))
                    Assert.Equal(expectedTxPower, (int)t[TwinProperty.TxPower]);
            })
        .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // todo add case without buffer
            for (int i = 0; i < messageCount; i++)
            {
                payloadFcnt = await this.SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor);
            }

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrl: (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADRAckReq + (int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, lsnr: currentLsnr, datr: currentDR).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
            Assert.True(request.ProcessingSucceeded);

            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            // We expect a mac command in the payload
            if (deviceId == 6 || deviceId == 1)
            {
                // In this case, no ADR adaptation is performed, so the message should be empty
                Assert.Equal(0, payloadDataDown.Frmpayload.Span.Length);
                Assert.Equal(0, payloadDataDown.Fopts.Span.Length);
            }
            else
            {
                Assert.Equal(5, payloadDataDown.Frmpayload.Span.Length);
                var decryptedPayload = payloadDataDown.PerformEncryption(simulatedDevice.NwkSKey);
                Assert.Equal(0, payloadDataDown.Fport.Span[0]);
                Assert.Equal((byte)CidEnum.LinkADRCmd, decryptedPayload[0]);
                var linkAdr = new LinkADRRequest(decryptedPayload);
                Assert.Equal(expectedDR, linkAdr.DataRate);
                Assert.Equal(expectedDR, loraDevice.DataRate);
                Assert.Equal(expectedTxPower, linkAdr.TxPower);
                Assert.Equal(expectedTxPower, loraDevice.TxPower);

                // in case no payload the mac is in the FRMPayload and is decrypted with NwkSKey
                Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
                Assert.False(payloadDataDown.IsConfirmed);
                Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);
                // 4. Frame counter up was updated
                Assert.Equal(payloadFcnt, loraDevice.FCntUp);

                // 5. Frame counter down is updated
                Assert.Equal(ExpectedDeviceFcntDown + 1, loraDevice.FCntDown);
                Assert.Equal(ExpectedDeviceFcntDown + 1, payloadDataDown.GetFcnt());

                // 6. Frame count has no pending changes
                Assert.False(loraDevice.HasFrameCountChanges);
            }
        }

        [Fact]
        public async Task Perform_TXPower_Adaptation_When_Needed()
        {
            uint deviceId = 44;
            int messageCount = 20;
            int payloadFcnt = 0;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 0;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(deviceId, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            int reportedDR = 0;
            int reportedTxPower = 0;

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
            .Callback<TwinCollection>((t) =>
            {
                if (t.Contains(TwinProperty.DataRate))
                    reportedDR = t[TwinProperty.DataRate].Value;
                if (t.Contains(TwinProperty.TxPower))
                    reportedTxPower = t[TwinProperty.TxPower].Value;
            })
        .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // ****************************************************
            // First part gives very good connectivity and ensure we set power to minimum and DR increase to 5
            // ****************************************************
            // set to very good lsnr and DR3
            float currentLsnr = 20;
            string currentDR = "SF9BW125";

            // todo add case without buffer
            for (int i = 0; i < messageCount; i++)
            {
                payloadFcnt = await this.SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor);
            }

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrl: (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADRAckReq + (int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, lsnr: currentLsnr, datr: currentDR).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            // We expect a mac command in the payload
            Assert.Equal(5, payloadDataDown.Frmpayload.Span.Length);
            var decryptedPayload = payloadDataDown.PerformEncryption(simulatedDevice.NwkSKey);
            Assert.Equal(0, payloadDataDown.Fport.Span[0]);
            Assert.Equal((byte)CidEnum.LinkADRCmd, decryptedPayload[0]);
            var linkAdr = new LinkADRRequest(decryptedPayload);
            Assert.Equal(5, linkAdr.DataRate);
            Assert.Equal(5, loraDevice.DataRate);
            Assert.Equal(5, reportedDR);
            Assert.Equal(7, linkAdr.TxPower);
            Assert.Equal(7, loraDevice.TxPower);
            Assert.Equal(7, reportedTxPower);

            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);
            // 4. Frame counter up was updated
            Assert.Equal(payloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.GetFcnt());

            // ****************************************************
            // Second part reduce connectivity and verify the DR stay to 5 and power set to max
            // ****************************************************
            currentLsnr = -50;
            // DR5
            currentDR = "SF7BW125";

            // todo add case without buffer
            for (int i = 0; i < messageCount; i++)
            {
                payloadFcnt = await this.SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor);
            }

            payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrl: (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADRAckReq + (int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
            rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, lsnr: currentLsnr, datr: currentDR).Rxpk[0];
            request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(request.ResponseDownlink);
            Assert.Equal(2, this.PacketForwarder.DownlinkMessages.Count);
            downlinkMessage = this.PacketForwarder.DownlinkMessages[1];
            payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            // We expect a mac command in the payload
            Assert.Equal(5, payloadDataDown.Frmpayload.Span.Length);
            decryptedPayload = payloadDataDown.PerformEncryption(simulatedDevice.NwkSKey);
            Assert.Equal(0, payloadDataDown.Fport.Span[0]);
            Assert.Equal((byte)CidEnum.LinkADRCmd, decryptedPayload[0]);
            linkAdr = new LinkADRRequest(decryptedPayload);
            Assert.Equal(5, linkAdr.DataRate);
            Assert.Equal(5, loraDevice.DataRate);
            Assert.Equal(5, reportedDR);
            Assert.Equal(0, linkAdr.TxPower);
            Assert.Equal(0, loraDevice.TxPower);
            Assert.Equal(0, reportedTxPower);

            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);
            // 4. Frame counter up was updated
            Assert.Equal(payloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 2, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 2, payloadDataDown.GetFcnt());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
            Assert.True(request.ProcessingSucceeded);
            // 6. Frame count has no pending changes
            Assert.False(loraDevice.HasFrameCountChanges);
        }

        [Fact]
        public async Task Perform_NbRep_Adaptation_When_Needed()
        {
            uint deviceId = 31;
            string currentDR = "SF8BW125";
            int currentLsnr = -20;
            int messageCount = 20;
            int payloadFcnt = 0;
            const int InitialDeviceFcntUp = 1;
            const int ExpectedDeviceFcntDown = 3;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(deviceId, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);
            int reportedNbRep = 0;
            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
            .Callback<TwinCollection>((t) =>
            {
                if (t.Contains(TwinProperty.NbRep))
                    reportedNbRep = (int)t[TwinProperty.NbRep];
            })
        .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);
            // ****************************************************
            // First part send messages with spaces in fcnt to simulate wrong network connectivity
            // ****************************************************
            // send a message with a fcnt every 4.
            for (int i = 0; i < messageCount; i++)
            {
                payloadFcnt = await this.SendMessage(currentLsnr, currentDR, payloadFcnt + 3, simulatedDevice, messageProcessor);
            }

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrl: (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADRAckReq + (int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, lsnr: currentLsnr, datr: currentDR).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
            Assert.True(request.ProcessingSucceeded);

            Assert.NotNull(request.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[0];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            // We expect a mac command in the payload
            Assert.Equal(5, payloadDataDown.Frmpayload.Span.Length);
            var decryptedPayload = payloadDataDown.PerformEncryption(simulatedDevice.NwkSKey);
            Array.Reverse(decryptedPayload);
            Assert.Equal(0, payloadDataDown.Fport.Span[0]);
            Assert.Equal((byte)CidEnum.LinkADRCmd, decryptedPayload[0]);
            var linkAdr = new LinkADRRequest(decryptedPayload);
            Assert.Equal(3, reportedNbRep);
            Assert.Equal(3, linkAdr.NbRep);
            Assert.Equal(3, loraDevice.NbRep);
            // ****************************************************
            // Second part send normal messages to decrease NbRep
            // ****************************************************
            // send a message with a fcnt every 1
            for (int i = 0; i < messageCount; i++)
            {
                payloadFcnt = await this.SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor);
            }

            payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrl: (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADRAckReq + (int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
            rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, lsnr: currentLsnr, datr: currentDR).Rxpk[0];
            request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
            Assert.True(request.ProcessingSucceeded);

            Assert.NotNull(request.ResponseDownlink);
            Assert.Equal(2, this.PacketForwarder.DownlinkMessages.Count);
            downlinkMessage = this.PacketForwarder.DownlinkMessages[1];
            payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            // We expect a mac command in the payload
            Assert.Equal(5, payloadDataDown.Frmpayload.Span.Length);
            decryptedPayload = payloadDataDown.PerformEncryption(simulatedDevice.NwkSKey);
            Assert.Equal(0, payloadDataDown.Fport.Span[0]);
            Assert.Equal((byte)CidEnum.LinkADRCmd, decryptedPayload[0]);
            linkAdr = new LinkADRRequest(decryptedPayload);
            Assert.Equal(2, reportedNbRep);
            Assert.Equal(2, linkAdr.NbRep);
            Assert.Equal(2, loraDevice.NbRep);

            // in case no payload the mac is in the FRMPayload and is decrypted with NwkSKey
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);
            // 4. Frame counter up was updated
            Assert.Equal(payloadFcnt, loraDevice.FCntUp);

            // ****************************************************
            // Third part send normal messages to decrease NbRep to 1
            // ****************************************************
            // send a message with a fcnt every 1
            for (int i = 0; i < messageCount; i++)
            {
                payloadFcnt = await this.SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor);
            }

            payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrl: (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADRAckReq + (int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
            rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, lsnr: currentLsnr, datr: currentDR).Rxpk[0];
            request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
            Assert.True(request.ProcessingSucceeded);

            Assert.NotNull(request.ResponseDownlink);
            Assert.Equal(3, this.PacketForwarder.DownlinkMessages.Count);
            downlinkMessage = this.PacketForwarder.DownlinkMessages[2];
            payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            // We expect a mac command in the payload
            Assert.Equal(5, payloadDataDown.Frmpayload.Span.Length);
            decryptedPayload = payloadDataDown.PerformEncryption(simulatedDevice.NwkSKey);
            Assert.Equal(0, payloadDataDown.Fport.Span[0]);
            Assert.Equal((byte)CidEnum.LinkADRCmd, decryptedPayload[0]);
            linkAdr = new LinkADRRequest(decryptedPayload);
            Assert.Equal(1, reportedNbRep);
            Assert.Equal(1, linkAdr.NbRep);
            Assert.Equal(1, loraDevice.NbRep);

            // 5. Frame counter down is updated
            Assert.Equal(ExpectedDeviceFcntDown, loraDevice.FCntDown);
            Assert.Equal(ExpectedDeviceFcntDown, payloadDataDown.GetFcnt());

            // 6. Frame count has no pending changes
            Assert.False(loraDevice.HasFrameCountChanges);
        }

        private async Task<int> SendMessage(float currentLsnr, string currentDR, int payloadFcnt, SimulatedDevice simulatedDevice, MessageDispatcher messageProcessor)
        {
            var payloadInt = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrl: (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
            var rxpkInt = payloadInt.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, lsnr: currentLsnr, datr: currentDR).Rxpk[0];
            var requestInt = this.CreateWaitableRequest(rxpkInt);
            messageProcessor.DispatchRequest(requestInt);
            Assert.True(await requestInt.WaitCompleteAsync());
            payloadFcnt++;
            return payloadFcnt;
        }
    }
}
