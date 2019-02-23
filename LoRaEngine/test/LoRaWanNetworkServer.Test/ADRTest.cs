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
        [InlineData(1, 70, true, 3, 7, 1)]
        [InlineData(2, 18, false, 0, 0, 0)]
        public async Task When_Receives_AdrAckReq_Send_MacCommand_Reply_When_Can_Do_Rate_Adaptation(uint deviceId, int count, bool expectADRApatation, int expectedDR, int expectedTXPower, int expectedNbRep)
        {
            int payloadFcnt = 0;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 2;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(deviceId, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

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
                Assert.True(await requestInt.WaitCompleteAsync());
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

            // 2. Return is downstream message$
            if (expectADRApatation)
            {
                Assert.NotNull(request.ResponseDownlink);
                Assert.True(request.ProcessingSucceeded);
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
                Assert.Equal(expectedDR, linkAdr.DataRate);
                Assert.Equal(expectedDR, loraDevice.DataRate);
                Assert.Equal(expectedTXPower, linkAdr.TxPower);
                Assert.Equal(expectedTXPower, loraDevice.TxPower);
                Assert.Equal(expectedNbRep, linkAdr.NbTrans);
                Assert.Equal(expectedNbRep, loraDevice.NbTrans);

                // in case no payload the mac is in the FRMPayload and is decrypted with NwkSKey
                Assert.Equal(payloadDataDown.DevAddr.ToArray(), LoRaTools.Utils.ConversionHelper.StringToByteArray(loraDevice.DevAddr));
                Assert.False(payloadDataDown.IsConfirmed);
                Assert.Equal(LoRaMessageType.UnconfirmedDataDown, payloadDataDown.LoRaMessageType);
                // 4. Frame counter up was updated
                Assert.Equal(payloadFcnt, loraDevice.FCntUp);

                // 5. Frame counter down is updated
                Assert.Equal(InitialDeviceFcntDown, loraDevice.FCntDown);
                Assert.Equal(InitialDeviceFcntDown, payloadDataDown.GetFcnt());

                // 6. Frame count has no pending changes
                Assert.False(loraDevice.HasFrameCountChanges);
            }
            else
            {
                // In this case we don't expect any answer.
                Assert.Null(request.ResponseDownlink);
                Assert.True(request.ProcessingSucceeded);
                Assert.Equal(payloadFcnt, loraDevice.FCntUp);
            }
        }

        [Theory]
        // deviceId, lsnr, inputDR, outputDR
        // DR5 with poor lsnr can't degrade DR
        // [InlineData(1, -20, "SF7BW125", 5, 7)]
        // DR0 with high lsnr will result in move to DR5
        [InlineData(2, 20, "SF12BW125", 5, 7)]
        // DR1 with high lsnr will result in move to DR5
        [InlineData(3, 20, "SF11BW125", 5, 7)]
        // DR3 with high lsnr will result in move to DR5
        [InlineData(4, 20, "SF9BW125", 5, 7)]
        // DR5 with high lsnr will result in lowering increase the TxPower to 7
        [InlineData(5, 20, "SF9BW125", 5, 7)]
        public async Task When_Receives_AdrAckReq_ADR_Change_DR(uint deviceId, float currentLsnr, string currentDR, int expectedDR, int expectedTxPower)
        {
            int messageCount = 20;
            int payloadFcnt = 0;
            const int InitialDeviceFcntUp = 9;
            const int InitialDeviceFcntDown = 2;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(deviceId, gatewayID: this.ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp,
                frmCntDown: InitialDeviceFcntDown);

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
            for (int i = 0; i < messageCount; i++)
            {
                var payloadInt = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrl: (byte)((int)LoRaTools.LoRaMessage.FctrlEnum.ADR));
                var rxpkInt = payloadInt.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, lsnr: currentLsnr, datr: currentDR).Rxpk[0];
                var requestInt = this.CreateWaitableRequest(rxpkInt);
                messageProcessor.DispatchRequest(requestInt);
                Assert.True(await requestInt.WaitCompleteAsync());
                payloadFcnt++;
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

            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
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
            Assert.Equal(InitialDeviceFcntDown, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown, payloadDataDown.GetFcnt());

            // 6. Frame count has no pending changes
            Assert.False(loraDevice.HasFrameCountChanges);
        }
    }
}
