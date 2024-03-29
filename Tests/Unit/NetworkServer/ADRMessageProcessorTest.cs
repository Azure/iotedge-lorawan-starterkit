// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools;
    using global::LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;
    using static DataRateIndex;

    public class ADRMessageProcessorTest : MessageProcessorTestBase
    {
        public ADRMessageProcessorTest(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

        [Theory]
        // deviceId, # messages sent, ExpectedDR, expectedPower, expectedNbRep
        // Enough Messages, Perform ADR
        [InlineData(10, 70, DR5, 7, 1, 9U)]
        // Not Enough Messages and fcnt too low, receives empty payload
        [InlineData(11, 15, DR2, 0, 1, 9U)]
        // Not Enough Messages and fcnt high, receives default values, stay on DR 2, max tx pow
        [InlineData(12, 15, DR2, 0, 1, 21U)]
        public async Task Perform_Rate_Adapatation_When_Possible(uint deviceId, int count, DataRateIndex expectedDR, int expectedTXPower, int expectedNbRep, uint initialDeviceFcntUp)
        {
            uint payloadFcnt = 0;
            const uint InitialDeviceFcntDown = 0;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(deviceId, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: initialDeviceFcntUp);

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
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

            // In this case we want to simulate a cache failure, so we don't initialize the cache.
            if (deviceId != 12)
            {
                payloadFcnt = await InitializeCacheToDefaultValuesAsync(payloadFcnt, simulatedDevice, messageProcessor);
            }
            else
            {
                var payloadInt = simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: payloadFcnt);
                using var requestInt = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), payloadInt);
                messageProcessor.DispatchRequest(requestInt);
                Assert.True(await requestInt.WaitCompleteAsync(-1));
                payloadFcnt++;
            }

            for (var i = 0; i < count; i++)
            {
                var payloadInt = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrlFlags: FrameControlFlags.Adr);
                using var requestInt = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), payloadInt);
                messageProcessor.DispatchRequest(requestInt);
                Assert.True(await requestInt.WaitCompleteAsync(-1));
                payloadFcnt++;
            }

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrlFlags: FrameControlFlags.AdrAckReq | FrameControlFlags.Adr);
            using var request = CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(), payload);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();

            Assert.NotNull(request.ResponseDownlink);
            Assert.True(request.ProcessingSucceeded);
            Assert.Equal(2, DownstreamMessageSender.DownlinkMessages.Count);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[1];
            var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);

            // in this case we expect a null payload
            if (deviceId == 11)
            {
                Assert.Equal(0, payloadDataDown.Frmpayload.Span.Length);
            }
            else
            {
                // We expect a mac command in the payload
                Assert.Equal(5, payloadDataDown.Frmpayload.Span.Length);
                var decryptedPayload = payloadDataDown.Serialize(simulatedDevice.NwkSKey.Value);
                Assert.Equal(FramePort.MacCommand, payloadDataDown.Fport);
                Assert.Equal((byte)Cid.LinkADRCmd, decryptedPayload[0]);
                var linkAdr = new LinkADRRequest(decryptedPayload);
                Assert.Equal(expectedDR, linkAdr.DataRate);
                Assert.Equal(expectedDR, loraDevice.DataRate);
                Assert.Equal(expectedTXPower, linkAdr.TxPower);
                Assert.Equal(expectedTXPower, loraDevice.TxPower);
                Assert.Equal(expectedNbRep, linkAdr.NbRep);
                Assert.Equal(expectedNbRep, loraDevice.NbRep);
            }

            // in case no payload the mac is in the FRMPayload and is decrypted with NwkSKey
            Assert.Equal(payloadDataDown.DevAddr, loraDevice.DevAddr);
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.MessageType);
            // 4. Frame counter up was updated
            Assert.Equal(payloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.Fcnt);
        }

        [Theory]
        // deviceId, lsnr, inputDR, outputDR
        // DR5 with poor lsnr can't degrade DR
        // [InlineData(221, -20, "SF7BW125", 5, 0)]
        // DR0 with high lsnr will result in move to DR5
        [InlineData(2, 20, "SF12BW125", DR5, 7)]
        // DR1 with high lsnr will result in move to DR5
        [InlineData(3, 20, "SF11BW125", DR5, 7)]
        // DR3 with high lsnr will result in move to DR5
        [InlineData(4, 20, "SF9BW125", DR5, 7)]
        // DR5 with high lsnr will result in reduce the TxPower to 7
        [InlineData(5, 20, "SF9BW125", DR5, 7)]
        // DR5 with low lsnr will not try to modify dr and already at maxTxPower
        [InlineData(6, -10, "SF7BW125", DR5, 0)]
        // Device 5 massive increase in txpower due to very bad lsnr
        [InlineData(5, -30, "SF9BW125", DR3, 0)]
        public async Task Perform_DR_Adaptation_When_Needed(uint deviceId, float currentLsnr, string currentDRString, DataRateIndex expectedDR, int expectedTxPower)
        {
            var currentDR = LoRaDataRate.Parse(currentDRString);
            var messageCount = 21;
            uint payloadFcnt = 0;
            const uint InitialDeviceFcntUp = 9;
            const uint ExpectedDeviceFcntDown = 0;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(deviceId, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp);

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var twinDR = DR0;
            var twinTxPower = 0;

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .Callback<TwinCollection, CancellationToken>((t, _) =>
                {
                    if (t.Contains(TwinProperty.DataRate))
                        twinDR = t[TwinProperty.DataRate];
                    if (t.Contains(TwinProperty.TxPower))
                        twinTxPower = (int)t[TwinProperty.TxPower];
                })
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

            payloadFcnt = await InitializeCacheToDefaultValuesAsync(payloadFcnt, simulatedDevice, messageProcessor);

            for (var i = 0; i < messageCount; i++)
            {
                payloadFcnt = await SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor, FrameControlFlags.Adr);
            }

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrlFlags: FrameControlFlags.AdrAckReq | FrameControlFlags.Adr);
            var radioMetadata = new RadioMetadata(TestUtils.TestRegion.GetDataRateIndex(currentDR), Hertz.Mega(868.1), new RadioMetadataUpInfo(0, 0, 0, 0, currentLsnr));
            using var request = CreateWaitableRequest(radioMetadata, payload);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
            Assert.True(request.ProcessingSucceeded);

            Assert.NotNull(request.ResponseDownlink);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[1];
            var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
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
                var decryptedPayload = payloadDataDown.Serialize(simulatedDevice.NwkSKey.Value);
                Assert.Equal(FramePort.MacCommand, payloadDataDown.Fport);
                Assert.Equal((byte)Cid.LinkADRCmd, decryptedPayload[0]);
                var linkAdr = new LinkADRRequest(decryptedPayload);
                Assert.Equal(expectedDR, linkAdr.DataRate);
                Assert.Equal(expectedDR, loraDevice.DataRate);
                Assert.Equal(expectedDR, twinDR);
                Assert.Equal(expectedTxPower, linkAdr.TxPower);
                Assert.Equal(expectedTxPower, loraDevice.TxPower);
                Assert.Equal(expectedTxPower, twinTxPower);

                // in case no payload the mac is in the FRMPayload and is decrypted with NwkSKey
                Assert.Equal(payloadDataDown.DevAddr, loraDevice.DevAddr);
                Assert.False(payloadDataDown.IsConfirmed);
                Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.MessageType);
                // 4. Frame counter up was updated
                Assert.Equal(payloadFcnt, loraDevice.FCntUp);

                // 5. Frame counter down is updated
                Assert.Equal(ExpectedDeviceFcntDown + 1, loraDevice.FCntDown);
                Assert.Equal(ExpectedDeviceFcntDown + 1, payloadDataDown.Fcnt);
            }
        }

        [Fact]
        public async Task Perform_TXPower_Adaptation_When_Needed()
        {
            uint deviceId = 44;
            var messageCount = 21;
            uint payloadFcnt = 0;
            const uint InitialDeviceFcntUp = 9;
            const uint InitialDeviceFcntDown = 0;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(deviceId, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp);

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            var reportedDR = DR0;
            var reportedTxPower = 0;

            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
            .Callback<TwinCollection, CancellationToken>((t, _) =>
            {
                if (t.Contains(TwinProperty.DataRate))
                    reportedDR = (DataRateIndex)(int)(object)t[TwinProperty.DataRate].Value;
                if (t.Contains(TwinProperty.TxPower))
                    reportedTxPower = t[TwinProperty.TxPower].Value;
            })
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
            payloadFcnt = await InitializeCacheToDefaultValuesAsync(payloadFcnt, simulatedDevice, messageProcessor);
            // ****************************************************
            // First part gives very good connectivity and ensure we set power to minimum and DR increase to 5
            // ****************************************************
            // set to very good lsnr and DR3
            float currentLsnr = 20;
            var currentDR = LoRaDataRate.SF9BW125;

            // todo add case without buffer
            for (var i = 0; i < messageCount; i++)
            {
                payloadFcnt = await SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor, FrameControlFlags.Adr);
            }

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrlFlags: FrameControlFlags.AdrAckReq | FrameControlFlags.Adr);
            var radioMetadata = new RadioMetadata(TestUtils.TestRegion.GetDataRateIndex(currentDR), Hertz.Mega(868.1), new RadioMetadataUpInfo(0, 0, 0, 0, currentLsnr));
            using var request = CreateWaitableRequest(radioMetadata, payload);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            Assert.NotNull(request.ResponseDownlink);
            Assert.Equal(2, DownstreamMessageSender.DownlinkMessages.Count);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[1];
            var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
            // We expect a mac command in the payload
            Assert.Equal(5, payloadDataDown.Frmpayload.Span.Length);
            var decryptedPayload = payloadDataDown.Serialize(simulatedDevice.NwkSKey.Value);
            Assert.Equal(FramePort.MacCommand, payloadDataDown.Fport);
            Assert.Equal((byte)Cid.LinkADRCmd, decryptedPayload[0]);
            var linkAdr = new LinkADRRequest(decryptedPayload);
            Assert.Equal(DR5, linkAdr.DataRate);
            Assert.Equal(DR5, loraDevice.DataRate);
            Assert.Equal(DR5, reportedDR);
            Assert.Equal(7, linkAdr.TxPower);
            Assert.Equal(7, loraDevice.TxPower);
            Assert.Equal(7, reportedTxPower);

            Assert.Equal(payloadDataDown.DevAddr, loraDevice.DevAddr);
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.MessageType);
            // 4. Frame counter up was updated
            Assert.Equal(payloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 1, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 1, payloadDataDown.Fcnt);

            // ****************************************************
            // Second part reduce connectivity and verify the DR stay to 5 and power set to max
            // ****************************************************
            currentLsnr = -50;
            // DR5
            currentDR = LoRaDataRate.SF7BW125;

            // todo add case without buffer
            for (var i = 0; i < messageCount; i++)
            {
                payloadFcnt = await SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor, FrameControlFlags.Adr);
            }

            payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrlFlags: FrameControlFlags.AdrAckReq | FrameControlFlags.Adr);
            var radioMetadataWithDatr5 = new RadioMetadata(TestUtils.TestRegion.GetDataRateIndex(currentDR), Hertz.Mega(868.1), new RadioMetadataUpInfo(0, 0, 0, 0, currentLsnr));
            using var secondRequest = CreateWaitableRequest(radioMetadataWithDatr5, payload);
            messageProcessor.DispatchRequest(secondRequest);
            Assert.True(await secondRequest.WaitCompleteAsync());

            Assert.NotNull(secondRequest.ResponseDownlink);
            Assert.Equal(3, DownstreamMessageSender.DownlinkMessages.Count);
            downlinkMessage = DownstreamMessageSender.DownlinkMessages[2];
            payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
            // We expect a mac command in the payload
            Assert.Equal(5, payloadDataDown.Frmpayload.Span.Length);
            decryptedPayload = payloadDataDown.Serialize(simulatedDevice.NwkSKey.Value);
            Assert.Equal(FramePort.MacCommand, payloadDataDown.Fport);
            Assert.Equal((byte)Cid.LinkADRCmd, decryptedPayload[0]);
            linkAdr = new LinkADRRequest(decryptedPayload);
            Assert.Equal(DR5, linkAdr.DataRate);
            Assert.Equal(DR5, loraDevice.DataRate);
            Assert.Equal(DR5, reportedDR);
            Assert.Equal(0, linkAdr.TxPower);
            Assert.Equal(0, loraDevice.TxPower);
            Assert.Equal(0, reportedTxPower);

            Assert.Equal(payloadDataDown.DevAddr, loraDevice.DevAddr);
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.MessageType);
            // 4. Frame counter up was updated
            Assert.Equal(payloadFcnt, loraDevice.FCntUp);

            // 5. Frame counter down is updated
            Assert.Equal(InitialDeviceFcntDown + 2, loraDevice.FCntDown);
            Assert.Equal(InitialDeviceFcntDown + 2, payloadDataDown.Fcnt);

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
            Assert.True(secondRequest.ProcessingSucceeded);
        }

        [Fact]
        public async Task Perform_NbRep_Adaptation_When_Needed()
        {
            uint deviceId = 31;
            var currentDR = LoRaDataRate.SF8BW125;
            var currentLsnr = -20;
            var messageCount = 20;
            uint payloadFcnt = 0;
            const uint InitialDeviceFcntUp = 1;
            const uint ExpectedDeviceFcntDown = 4;

            var simulatedDevice = new SimulatedDevice(
                TestDeviceInfo.CreateABPDevice(deviceId, gatewayID: ServerConfiguration.GatewayID),
                frmCntUp: InitialDeviceFcntUp);

            var loraDevice = CreateLoRaDevice(simulatedDevice);

            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);
            var reportedNbRep = 0;
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                            .Callback<TwinCollection, CancellationToken>((t, _) =>
                                {
                                    if (t.Contains(TwinProperty.NbRep))
                                        reportedNbRep = (int)t[TwinProperty.NbRep];
                                })
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

            payloadFcnt = await InitializeCacheToDefaultValuesAsync(payloadFcnt, simulatedDevice, messageProcessor);

            // ****************************************************
            // First part send messages with spaces in fcnt to simulate wrong network connectivity
            // ****************************************************
            // send a message with a fcnt every 4.
            for (var i = 0; i < messageCount; i++)
            {
                payloadFcnt = await SendMessage(currentLsnr, currentDR, payloadFcnt + 3, simulatedDevice, messageProcessor, FrameControlFlags.Adr);
            }

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrlFlags: FrameControlFlags.AdrAckReq | FrameControlFlags.Adr);
            var radioMetadata = new RadioMetadata(TestUtils.TestRegion.GetDataRateIndex(currentDR), Hertz.Mega(868.1), new RadioMetadataUpInfo(0, 0, 0, 0, currentLsnr));

            using var request = CreateWaitableRequest(radioMetadata, payload);
            messageProcessor.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
            Assert.True(request.ProcessingSucceeded);

            Assert.NotNull(request.ResponseDownlink);
            Assert.Equal(2, DownstreamMessageSender.DownlinkMessages.Count);
            var downlinkMessage = DownstreamMessageSender.DownlinkMessages[1];
            var payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
            // We expect a mac command in the payload
            Assert.Equal(5, payloadDataDown.Frmpayload.Span.Length);
            var decryptedPayload = payloadDataDown.Serialize(simulatedDevice.NwkSKey.Value);
            Assert.Equal(FramePort.MacCommand, payloadDataDown.Fport);
            Assert.Equal((byte)Cid.LinkADRCmd, decryptedPayload[0]);
            var linkAdr = new LinkADRRequest(decryptedPayload);
            Assert.Equal(3, reportedNbRep);
            Assert.Equal(3, linkAdr.NbRep);
            Assert.Equal(3, loraDevice.NbRep);
            // ****************************************************
            // Second part send normal messages to decrease NbRep
            // ****************************************************
            // send a message with a fcnt every 1
            for (var i = 0; i < messageCount; i++)
            {
                payloadFcnt = await SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor, FrameControlFlags.Adr);
            }

            payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrlFlags: FrameControlFlags.AdrAckReq | FrameControlFlags.Adr);
            using var secondRequest = CreateWaitableRequest(radioMetadata, payload);
            messageProcessor.DispatchRequest(secondRequest);
            Assert.True(await secondRequest.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
            Assert.True(secondRequest.ProcessingSucceeded);

            Assert.NotNull(secondRequest.ResponseDownlink);
            Assert.Equal(3, DownstreamMessageSender.DownlinkMessages.Count);
            downlinkMessage = DownstreamMessageSender.DownlinkMessages[2];
            payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
            // We expect a mac command in the payload
            Assert.Equal(5, payloadDataDown.Frmpayload.Span.Length);
            decryptedPayload = payloadDataDown.Serialize(simulatedDevice.NwkSKey.Value);
            Assert.Equal(FramePort.MacCommand, payloadDataDown.Fport);
            Assert.Equal((byte)Cid.LinkADRCmd, decryptedPayload[0]);
            linkAdr = new LinkADRRequest(decryptedPayload);
            Assert.Equal(2, reportedNbRep);
            Assert.Equal(2, linkAdr.NbRep);
            Assert.Equal(2, loraDevice.NbRep);

            // in case no payload the mac is in the FRMPayload and is decrypted with NwkSKey
            Assert.Equal(payloadDataDown.DevAddr, loraDevice.DevAddr);
            Assert.False(payloadDataDown.IsConfirmed);
            Assert.Equal(MacMessageType.UnconfirmedDataDown, payloadDataDown.MessageType);
            // 4. Frame counter up was updated
            Assert.Equal(payloadFcnt, loraDevice.FCntUp);

            // ****************************************************
            // Third part send normal messages to decrease NbRep to 1
            // ****************************************************
            // send a message with a fcnt every 1
            for (var i = 0; i < messageCount; i++)
            {
                payloadFcnt = await SendMessage(currentLsnr, currentDR, payloadFcnt, simulatedDevice, messageProcessor, FrameControlFlags.Adr);
            }

            payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrlFlags: FrameControlFlags.AdrAckReq | FrameControlFlags.Adr);
            using var thirdRequest = CreateWaitableRequest(radioMetadata, payload);
            messageProcessor.DispatchRequest(thirdRequest);
            Assert.True(await thirdRequest.WaitCompleteAsync());

            // Expectations
            // 1. Message was sent to IoT Hub
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
            Assert.True(thirdRequest.ProcessingSucceeded);

            Assert.NotNull(thirdRequest.ResponseDownlink);
            Assert.Equal(4, DownstreamMessageSender.DownlinkMessages.Count);
            downlinkMessage = DownstreamMessageSender.DownlinkMessages[3];
            payloadDataDown = new LoRaPayloadData(downlinkMessage.Data);
            // We expect a mac command in the payload
            Assert.Equal(5, payloadDataDown.Frmpayload.Span.Length);
            decryptedPayload = payloadDataDown.Serialize(simulatedDevice.NwkSKey.Value);
            Assert.Equal(FramePort.MacCommand, payloadDataDown.Fport);
            Assert.Equal((byte)Cid.LinkADRCmd, decryptedPayload[0]);
            linkAdr = new LinkADRRequest(decryptedPayload);
            Assert.Equal(1, reportedNbRep);
            Assert.Equal(1, linkAdr.NbRep);
            Assert.Equal(1, loraDevice.NbRep);

            // 5. Frame counter down is updated
            Assert.Equal(ExpectedDeviceFcntDown, loraDevice.FCntDown);
            Assert.Equal(ExpectedDeviceFcntDown, payloadDataDown.Fcnt);
        }

        private async Task<uint> SendMessage(float currentLsnr, LoRaDataRate currentDR, uint payloadFcnt, SimulatedDevice simulatedDevice, MessageDispatcher messageProcessor, FrameControlFlags fctrl)
        {
            var payloadInt = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: payloadFcnt, fctrlFlags: fctrl);
            using var requestInt = CreateWaitableRequest(new RadioMetadata(TestUtils.TestRegion.GetDataRateIndex(currentDR), Hertz.Mega(868.1), new RadioMetadataUpInfo(0,0,0,0,currentLsnr)) ,payloadInt);
            messageProcessor.DispatchRequest(requestInt);
            Assert.True(await requestInt.WaitCompleteAsync(-1));
            payloadFcnt++;
            return payloadFcnt;
        }

        private async Task<uint> InitializeCacheToDefaultValuesAsync(uint payloadfcnt, SimulatedDevice simulatedDevice, MessageDispatcher messageProcessor)
        {
            return await SendMessage(0, LoRaDataRate.SF7BW125, payloadfcnt, simulatedDevice, messageProcessor, FrameControlFlags.Adr | FrameControlFlags.AdrAckReq);
        }
    }
}
