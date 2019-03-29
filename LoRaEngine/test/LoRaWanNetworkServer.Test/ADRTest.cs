// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
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
        // deviceId, # messages sent, ExpectedDR, expectedPower, expectedNbRep
        // Enough Messages, Perform ADR
        [InlineData(10, 70, 5, 7, 1, 9U)]
        // Not Enough Messages and fcnt too low, receives empty payload
        [InlineData(11, 15, 2, 0, 1, 9U)]
        // Not Enough Messages and fcnt high, receives default values, stay on DR 2, max tx pow
        [InlineData(12, 15, 2, 0, 1, 21U)]
        public async Task Perform_Rate_Adapatation_When_Possible(uint deviceId, int count, int expectedDR, int expectedTXPower, int expectedNbRep, uint initialDeviceFcntUp)
        {
            uint payloadFcnt = 0;
            const uint InitialDeviceFcntDown = 0;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(deviceId, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: initialDeviceFcntUp);

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

            // In this case we want to simulate a cache failure, so we don't initialize the cache.
            if (deviceId != 12)
            {
                payloadFcnt = await this.InitializeCacheToDefaultValuesAsync(payloadFcnt, simulatedDevice, messageProcessor);
            }
            else
            {
                var payloadInt = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: payloadFcnt);
                var rxpkInt = payloadInt.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
                var requestInt = this.CreateWaitableRequest(rxpkInt);
                messageProcessor.DispatchRequest(requestInt);
                Assert.True(await requestInt.WaitCompleteAsync(-1));
                payloadFcnt++;
            }

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
            Assert.Equal(2, this.PacketForwarder.DownlinkMessages.Count);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[1];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));

            // in this case we expect a null payload
            if (deviceId == 11)
            {
                Assert.Equal(0, payloadDataDown.Frmpayload.Span.Length);
            }
            else
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
            }

            // in case no payload the mac is in the FRMPayload and is decrypted with NwkSKey
            Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);
            // 4. Frame counter up was updated
            Assert.Equal(payloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.GetFcnt());
        }

        [Theory]
        // deviceId, lsnr, inputDR, outputDR
        // DR5 with poor lsnr can't degrade DR
        // [InlineData(221, -20, "SF7BW125", 5, 0)]
        // DR0 with high lsnr will result in move to DR5
        [InlineData(2, 20, "SF12BW125", 5, 7)]
        // DR1 with high lsnr will result in move to DR5
        [InlineData(3, 20, "SF11BW125", 5, 7)]
        // DR3 with high lsnr will result in move to DR5
        [InlineData(4, 20, "SF9BW125", 5, 7)]
        // DR5 with high lsnr will result in reduce the TxPower to 7
        [InlineData(5, 20, "SF9BW125", 5, 7)]
        // DR5 with low lsnr will not try to modify dr and already at maxTxPower
        [InlineData(6, -10, "SF7BW125", 5, 0)]
        // Device 5 massive increase in txpower due to very bad lsnr
        [InlineData(5, -30, "SF9BW125", 3, 0)]
        public async Task Perform_DR_Adaptation_When_Needed(uint deviceId, float currentLsnr, string currentDR, int expectedDR, int expectedTxPower)
        {
            int messageCount = 21;
            uint payloadFcnt = 0;
            const uint InitialDeviceFcntUp = 9;
            const uint ExpectedDeviceFcntDown = 0;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(deviceId, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp);

            var loraDevice = this.CreateLoRaDevice(simulatedDevice);

            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            int twinDR = 0;
            int twinTxPower = 0;

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .Callback<TwinCollection>((t) =>
                {
                    if (t.Contains(TwinProperty.DataRate))
                        twinDR = t[TwinProperty.DataRate];
                    if (t.Contains(TwinProperty.TxPower))
                        twinTxPower = (int)t[TwinProperty.TxPower];
                })
                .ReturnsAsync(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(loraDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            payloadFcnt = await this.InitializeCacheToDefaultValuesAsync(payloadFcnt, simulatedDevice, messageProcessor);

            for (int i = 0; i < messageCount; i++)
            {
                payloadFcnt = await this.SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor, (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
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
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[1];
            var payloadDataDown = new LoRaPayloadData(Convert.FromBase64String(downlinkMessage.Txpk.Data));
            // We expect a mac command in the payload
            if (deviceId == 221)
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
                Assert.Equal(expectedDR, twinDR);
                Assert.Equal(expectedTxPower, linkAdr.TxPower);
                Assert.Equal(expectedTxPower, loraDevice.TxPower);
                Assert.Equal(expectedTxPower, twinTxPower);

                // in case no payload the mac is in the FRMPayload and is decrypted with NwkSKey
                Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
                Assert.False(payloadDataDown.IsConfirmed);
                Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);
                // 4. Frame counter up was updated
                Assert.Equal(payloadFcnt, loraDevice.FCntUp);

                // 5. Frame counter down is updated
                Assert.Equal(ExpectedDeviceFcntDown + 1, loraDevice.FCntDown);
                Assert.Equal(ExpectedDeviceFcntDown + 1, payloadDataDown.GetFcnt());
            }
        }

        [Fact]
        public async Task Perform_TXPower_Adaptation_When_Needed()
        {
            uint deviceId = 44;
            int messageCount = 21;
            uint payloadFcnt = 0;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 0;

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
            payloadFcnt = await this.InitializeCacheToDefaultValuesAsync(payloadFcnt, simulatedDevice, messageProcessor);
            // ****************************************************
            // First part gives very good connectivity and ensure we set power to minimum and DR increase to 5
            // ****************************************************
            // set to very good lsnr and DR3
            float currentLsnr = 20;
            string currentDR = "SF9BW125";

            // todo add case without buffer
            for (int i = 0; i < messageCount; i++)
            {
                payloadFcnt = await this.SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor, (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
            }

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrl: (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADRAckReq + (int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
            var rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, lsnr: currentLsnr, datr: currentDR).Rxpk[0];
            var request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(request.ResponseDownlink);
            Assert.Equal(2, this.PacketForwarder.DownlinkMessages.Count);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[1];
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
                payloadFcnt = await this.SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor, (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
            }

            payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrl: (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADRAckReq + (int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
            rxpk = payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, lsnr: currentLsnr, datr: currentDR).Rxpk[0];
            request = this.CreateWaitableRequest(rxpk);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

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
        }

        [Fact]
        public async Task Perform_NbRep_Adaptation_When_Needed()
        {
            uint deviceId = 31;
            string currentDR = "SF8BW125";
            int currentLsnr = -20;
            int messageCount = 20;
            uint payloadFcnt = 0;
            const uint InitialDeviceFcntUp = 1;
            const uint ExpectedDeviceFcntDown = 4;

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

            payloadFcnt = await this.InitializeCacheToDefaultValuesAsync(payloadFcnt, simulatedDevice, messageProcessor);

            // ****************************************************
            // First part send messages with spaces in fcnt to simulate wrong network connectivity
            // ****************************************************
            // send a message with a fcnt every 4.
            for (int i = 0; i < messageCount; i++)
            {
                payloadFcnt = await this.SendMessage(currentLsnr, currentDR, payloadFcnt + 3, simulatedDevice, messageProcessor, (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
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
            Assert.Equal(2, this.PacketForwarder.DownlinkMessages.Count);
            var downlinkMessage = this.PacketForwarder.DownlinkMessages[1];
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
                payloadFcnt = await this.SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor, (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
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
                payloadFcnt = await this.SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor, (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
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
            Assert.Equal(4, this.PacketForwarder.DownlinkMessages.Count);
            downlinkMessage = this.PacketForwarder.DownlinkMessages[3];
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
        }

        private async Task<uint> SendMessage(float currentLsnr, string currentDR, uint payloadFcnt, SimulatedDevice simulatedDevice, MessageDispatcher messageProcessor, byte fctrl)
        {
            var payloadInt = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrl: fctrl);
            var rxpkInt = payloadInt.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, lsnr: currentLsnr, datr: currentDR).Rxpk[0];
            var requestInt = this.CreateWaitableRequest(rxpkInt);
            messageProcessor.DispatchRequest(requestInt);
            Assert.True(await requestInt.WaitCompleteAsync(-1));
            payloadFcnt++;
            return payloadFcnt;
        }

        private async Task<uint> InitializeCacheToDefaultValuesAsync(uint payloadfcnt, SimulatedDevice simulatedDevice, MessageDispatcher messageProcessor)
        {
            return await this.SendMessage(0, "SF7BW125", payloadfcnt, simulatedDevice, messageProcessor, (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADR + (int)LoRaTools.LoRaMessage.FctrlEnum.ADRAckReq));
        }
    }
}
