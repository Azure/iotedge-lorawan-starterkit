// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Only join tests
    public class JoinTests : MessageProcessorTestBase
    {
        public JoinTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

        [Theory]
        [InlineData(ServerGatewayID, 200, 50, 0, 0)]
        [InlineData(ServerGatewayID, 200, 50, 17, 1)]
        [InlineData(ServerGatewayID, 0, 0, 0, 255)]
        [InlineData(ServerGatewayID, 0, 0, 27, 125)]
        [InlineData(null, 200, 50, 0, 1000)]
        [InlineData(null, 200, 50, 37, 28)]
        [InlineData(null, 0, 0, 0, 23)]
        [InlineData(null, 0, 0, 47, 10000)]
        public Task Join_And_Send_Unconfirmed_And_Confirmed_Messages(string deviceGatewayID, uint initialFcntUp, uint initialFcntDown, uint startingPayloadFcnt, int netId) =>
            Join_With_Subsequent_Unconfirmed_And_Confirmed_Messages(deviceGatewayID, initialFcntUp, initialFcntDown, startingPayloadFcnt, netId, DefaultRegion);

        public static TheoryData<Region> Join_Succeeds_For_All_Regions_TheoryData() => TheoryDataFactory.From(Join_Succeeds_For_All_Regions_InternalTheoryData());

        private static IEnumerable<Region> Join_Succeeds_For_All_Regions_InternalTheoryData()
        {
            yield return RegionManager.CN470RP1;
            yield return RegionManager.CN470RP2;
            yield return RegionManager.US915;
            var as923 = (RegionAS923)RegionManager.AS923;
            as923.DesiredDwellTimeSetting = new DwellTimeSetting(true, true, 5);
            yield return as923;
        }

        [Theory]
        [MemberData(nameof(Join_Succeeds_For_All_Regions_TheoryData))]
        public Task Join_Succeeds_For_All_Regions(Region region) =>
            Join_With_Subsequent_Unconfirmed_And_Confirmed_Messages(ServerGatewayID, 200, 50, 0, 0, region);

        private async Task Join_With_Subsequent_Unconfirmed_And_Confirmed_Messages(string deviceGatewayID, uint initialFcntUp, uint initialFcntDown, uint startingPayloadFcnt, int netId, Region region)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequestPayload = simulatedDevice.CreateJoinRequest();

            var devAddr = (DevAddr?)null;
            var devEui = simulatedDevice.LoRaDevice.DevEui;

            ServerConfiguration.NetId = new NetId(netId);
            // Device twin will be queried
            var twin = LoRaDeviceTwin.Create(
                simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties(),
                new LoRaReportedTwinProperties
                {
                    FCntUp = initialFcntUp,
                    FCntDown = initialFcntDown
                });
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            // Device twin will be updated
            AppSessionKey? afterJoinAppSKey = null;
            NetworkSessionKey? afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            uint afterJoinFcntDown = 0;
            uint afterJoinFcntUp = 0;

            TwinCollection actualSavedTwin = null;
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .Callback<TwinCollection, CancellationToken>((updatedTwin, _) =>
                {
                    if (updatedTwin.Contains(TwinProperty.AppSKey)) afterJoinAppSKey = AppSessionKey.Parse(updatedTwin[TwinProperty.AppSKey].Value);
                    if (updatedTwin.Contains(TwinProperty.NwkSKey)) afterJoinNwkSKey = NetworkSessionKey.Parse(updatedTwin[TwinProperty.NwkSKey].Value);
                    if (updatedTwin.Contains(TwinProperty.DevAddr)) afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr];
                    afterJoinFcntDown = updatedTwin[TwinProperty.FCntDown];
                    afterJoinFcntUp = updatedTwin[TwinProperty.FCntUp];
                    actualSavedTwin = updatedTwin;
                })
                .ReturnsAsync(true);

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => sentTelemetry.Add(t))
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequestPayload.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            // multi gateway will request a next frame count down from the lora device api, prepare it
            if (string.IsNullOrEmpty(deviceGatewayID))
            {
                LoRaDeviceApi.Setup(x => x.NextFCntDownAsync(devEui, 0, startingPayloadFcnt + 1, ServerConfiguration.GatewayID))
                    .ReturnsAsync((ushort)1);
                LoRaDeviceApi
                    .Setup(x => x.ExecuteFunctionBundlerAsync(devEui, It.IsAny<FunctionBundlerRequest>()))
                    .ReturnsAsync(() => new FunctionBundlerResult
                    {
                        AdrResult = new LoRaTools.ADR.LoRaADRResult
                        {
                            CanConfirmToDevice = false,
                            FCntDown = 1,
                            NbRepetition = 1,
                            TxPower = 0
                        },
                        NextFCntDown = 1
                    });
            }

            // using factory to create mock of
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // Create a join request and join with the device.
            using var joinRequest =
                CreateWaitableRequest(joinRequestPayload, constantElapsedTime: TimeSpan.FromMilliseconds(300), region: region);
            messageProcessor.DispatchRequest(joinRequest);
            Assert.True(await joinRequest.WaitCompleteAsync());
            Assert.True(joinRequest.ProcessingSucceeded, $"Failed due to '{joinRequest.ProcessingFailedReason}'.");
            Assert.NotNull(joinRequest.ResponseDownlink);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downlinkJoinAcceptMessage = DownstreamMessageSender.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(downlinkJoinAcceptMessage.Data, simulatedDevice.LoRaDevice.AppKey.Value);
            Assert.Equal(joinAccept.DevAddr.ToString(), afterJoinDevAddr);

            // check that the device is in cache
            Assert.True(DeviceCache.TryGetByDevEui(devEui, out var loRaDevice));
            Assert.Equal(afterJoinAppSKey, loRaDevice.AppSKey);
            Assert.Equal(afterJoinNwkSKey, loRaDevice.NwkSKey);
            Assert.Equal(afterJoinDevAddr, loRaDevice.DevAddr.ToString());
            var netIdBytes = BitConverter.GetBytes(netId);
            Assert.Equal(netIdBytes[0] & 0b01111111, DevAddr.Parse(afterJoinDevAddr).NetworkId);
            if (deviceGatewayID == null)
                Assert.Null(loRaDevice.GatewayID);
            else
                Assert.Equal(deviceGatewayID, loRaDevice.GatewayID);

            // Assert that after a join the fcnt is restarted
            Assert.Equal(0U, afterJoinFcntDown);
            Assert.Equal(0U, afterJoinFcntUp);
            Assert.Equal(0U, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.FCntDown);
            Assert.False(loRaDevice.HasFrameCountChanges);

            simulatedDevice.LoRaDevice.AppSKey = afterJoinAppSKey;
            simulatedDevice.LoRaDevice.NwkSKey = afterJoinNwkSKey;
            simulatedDevice.LoRaDevice.DevAddr = DevAddr.Parse(afterJoinDevAddr);

            // sends unconfirmed message with a given starting frame counter
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("100", fcnt: startingPayloadFcnt);
            var radioMetadata = TestUtils.GenerateTestRadioMetadata();
            using var unconfirmedRequest =
                CreateWaitableRequest(radioMetadata, unconfirmedMessagePayload,
                                      constantElapsedTime: TimeSpan.FromMilliseconds(300));
            messageProcessor.DispatchRequest(unconfirmedRequest);
            Assert.True(await unconfirmedRequest.WaitCompleteAsync());
            Assert.Null(unconfirmedRequest.ResponseDownlink);
            Assert.True(unconfirmedRequest.ProcessingSucceeded);

            // fcnt up was updated
            Assert.Equal(startingPayloadFcnt, loRaDevice.FCntUp);
            Assert.Equal(string.IsNullOrEmpty(deviceGatewayID) ? 1U : 0U, loRaDevice.FCntDown);

            // If the starting payload was not 0, it is expected that it updates the framecounter char
            // The device will perform the frame counter update and at this point in time it will have the same frame counter as the desired
            // Therefore savechangesasync will set the hasframcounter change to false
            // if (startingPayloadFcnt != 0)
            // {
            //    // Frame change flag will be set, only saving every 10 messages
            //    Assert.True(loRaDevice.HasFrameCountChanges);
            // }

            Assert.Single(sentTelemetry);

            // sends confirmed message
            var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("200", fcnt: startingPayloadFcnt + 1);
            using var confirmedRequest = CreateWaitableRequest(confirmedMessagePayload, constantElapsedTime: TimeSpan.FromMilliseconds(300), region: region);
            messageProcessor.DispatchRequest(confirmedRequest);
            Assert.True(await confirmedRequest.WaitCompleteAsync());
            Assert.True(confirmedRequest.ProcessingSucceeded);
            Assert.NotNull(confirmedRequest.ResponseDownlink);
            Assert.Equal(2, DownstreamMessageSender.DownlinkMessages.Count);
            Assert.Equal(2, sentTelemetry.Count);
            var downstreamMessage = DownstreamMessageSender.DownlinkMessages[1];

            // validates txpk according to region
            DeviceJoinInfo deviceJoinInfo = null;
            if (region is RegionCN470RP2 cnRegion && cnRegion.TryGetJoinChannelIndex(confirmedRequest.RadioMetadata.Frequency, out var channelIndex))
            {
                deviceJoinInfo = new DeviceJoinInfo(channelIndex);
            }
            Assert.True(region.TryGetDownstreamChannelFrequency(confirmedRequest.RadioMetadata.Frequency, confirmedRequest.RadioMetadata.DataRate, deviceJoinInfo, downstreamFrequency: out var frequency));
            Assert.Equal(frequency, downstreamMessage.Rx1?.Frequency);

            var rx2Freq = region.GetDownstreamRX2Freq(null, deviceJoinInfo, NullLogger.Instance);
            Assert.Equal(rx2Freq, downstreamMessage.Rx2.Frequency);

            var rx2DataRate = region.GetDownstreamRX2DataRate(null, null, deviceJoinInfo, NullLogger.Instance);
            Assert.Equal(rx2DataRate, downstreamMessage.Rx2.DataRate);

            // fcnt up was updated
            Assert.Equal(startingPayloadFcnt + 1, loRaDevice.FCntUp);
            Assert.Equal(1U, loRaDevice.FCntDown);

            // Frame change flag will be set, only saving every 10 messages
            Assert.True(loRaDevice.HasFrameCountChanges);

            // C2D message will be checked twice (for AS923 only once, since we use the first C2D message to send the dwell time MAC command)
            var numberOfC2DMessageChecks = region is RegionAS923 ? 1 : 2;
            LoRaDeviceClient.Verify(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()), Times.Exactly(numberOfC2DMessageChecks));

            // has telemetry with both fcnt
            Assert.Single(sentTelemetry, (t) => t.Fcnt == startingPayloadFcnt);
            Assert.Single(sentTelemetry, (t) => t.Fcnt == (startingPayloadFcnt + 1));

            // should not save class C device properties
            Assert.False(actualSavedTwin.Contains(TwinProperty.Region));
            Assert.False(actualSavedTwin.Contains(TwinProperty.PreferredGatewayID));

            LoRaDeviceClient.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Join_Fails_Due_To_GetTwin_Error_Second_Try_Should_Reload_Device_Twin(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequestPayload1 = simulatedDevice.CreateJoinRequest();

            var devAddr = (DevAddr?)null;
            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // Device twin will be queried
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties());
            LoRaDeviceClient.SetupSequence(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync((Twin)null)
                .ReturnsAsync(twin);

            // Device twin will be updated
            AppSessionKey? afterJoinAppSKey = null;
            NetworkSessionKey? afterJoinNwkSKey = null;
            string afterJoinDevAddr = null;
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .Callback<TwinCollection, CancellationToken>((updatedTwin, _) =>
                {
                    afterJoinAppSKey = AppSessionKey.Parse(updatedTwin[TwinProperty.AppSKey].Value);
                    afterJoinNwkSKey = NetworkSessionKey.Parse(updatedTwin[TwinProperty.NwkSKey].Value);
                    afterJoinDevAddr = updatedTwin[TwinProperty.DevAddr];
                })
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequestPayload1.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            // using factory to create mock of
            using var cache = NewMemoryCache();
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // 1st join request
            // Should fail
            using var joinRequest1 = CreateWaitableRequest(joinRequestPayload1);
            messageProcessor.DispatchRequest(joinRequest1);
            Assert.True(await joinRequest1.WaitCompleteAsync());
            Assert.True(joinRequest1.ProcessingFailed);
            Assert.Null(joinRequest1.ResponseDownlink);

            // 2nd attempt
            var joinRequestPayload2 = simulatedDevice.CreateJoinRequest();

            // will reload the device matched by deveui
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequestPayload2.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            using var joinRequest2 = CreateWaitableRequest(joinRequestPayload2);
            messageProcessor.DispatchRequest(joinRequest2);
            Assert.True(await joinRequest2.WaitCompleteAsync());
            Assert.NotNull(joinRequest2.ResponseDownlink);
            Assert.True(joinRequest2.ProcessingSucceeded);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var joinRequestDownlinkMessage2 = DownstreamMessageSender.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(joinRequestDownlinkMessage2.Data, simulatedDevice.LoRaDevice.AppKey.Value);
            Assert.Equal(joinAccept.DevAddr.ToString(), afterJoinDevAddr);

            Assert.True(DeviceCache.TryGetByDevEui(devEui, out var loRaDevice));

            Assert.Equal(simulatedDevice.AppKey, loRaDevice.AppKey);
            Assert.Equal(simulatedDevice.AppEui, loRaDevice.AppEui);
            Assert.Equal(afterJoinAppSKey, loRaDevice.AppSKey);
            Assert.Equal(afterJoinNwkSKey, loRaDevice.NwkSKey);
            Assert.Equal(afterJoinDevAddr, loRaDevice.DevAddr.ToString());
            if (deviceGatewayID == null)
                Assert.Null(loRaDevice.GatewayID);
            else
                Assert.Equal(deviceGatewayID, loRaDevice.GatewayID);

            // fcnt is restarted
            Assert.Equal(0U, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.FCntDown);
            Assert.False(loRaDevice.HasFrameCountChanges);

            // should get twin 2x (1st failed)
            LoRaDeviceClient.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Exactly(2));

            // should get device for join 2x
            LoRaDeviceApi.Verify(x => x.SearchAndLockForJoinAsync(ServerGatewayID, devEui, It.IsAny<DevNonce>()));

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task Join_Device_Has_Mismatching_AppEUI_Should_Return_Null(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequestPayload = simulatedDevice.CreateJoinRequest();

            var devAddr = (DevAddr?)null;
            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // Device twin will be queried
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties() with
            {
                JoinEui = new JoinEui(01213147654)
            });
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequestPayload.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            // using factory to create mock of
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // join request should fail
            using var joinRequest = CreateWaitableRequest(joinRequestPayload);
            messageProcessor.DispatchRequest(joinRequest);
            Assert.True(await joinRequest.WaitCompleteAsync());
            Assert.Null(joinRequest.ResponseDownlink);

            LoRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>()), Times.Never);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task Join_Device_Has_Mismatching_AppKey_Should_Return_Null(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequestPayload = simulatedDevice.CreateJoinRequest();

            var devAddr = (DevAddr?)null;
            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // Device twin will be queried
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties() with
            {
                AppKey = TestKeys.CreateAppKey(121314765654),
            });
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequestPayload.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            // using factory to create mock of
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // join request should fail
            using var joinRequest = CreateWaitableRequest(joinRequestPayload);
            messageProcessor.DispatchRequest(joinRequest);
            Assert.True(await joinRequest.WaitCompleteAsync());
            Assert.True(joinRequest.ProcessingFailed);
            Assert.Null(joinRequest.ResponseDownlink);

            LoRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>(), It.IsAny<CancellationToken>()), Times.Never);
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        /// <summary>
        /// Downlink should use same rfch than uplink message
        /// RFCH stands for Concentrator "RF chain" used for RX.
        /// </summary>
        [Theory]
        [InlineData(ServerGatewayID, 1)]
        [InlineData(ServerGatewayID, 0)]
        [InlineData(null, 1)]
        [InlineData(null, 0)]
        public async Task OTAA_Join_Should_Use_Rchf_0(string deviceGatewayID, uint rfch)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequestPayload = simulatedDevice.CreateJoinRequest();

            var devAddr = (DevAddr?)null;
            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // Device twin will be queried
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties());
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(twin);

            // Device twin will be updated
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequestPayload.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            // using factory to create mock of

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);
            var radio = TestUtils.GenerateTestRadioMetadata(antennaPreference: rfch);
            using var joinRequest = CreateWaitableRequest(radio, joinRequestPayload);
            messageProcessor.DispatchRequest(joinRequest);
            Assert.True(await joinRequest.WaitCompleteAsync());
            Assert.True(joinRequest.ProcessingSucceeded);
            Assert.NotNull(joinRequest.ResponseDownlink);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var downlinkJoinAcceptMessage = DownstreamMessageSender.DownlinkMessages[0];
            // validates txpk according to eu region
            Assert.True(RegionManager.EU868.TryGetDownstreamChannelFrequency(radio.Frequency, joinRequest.RadioMetadata.DataRate, deviceJoinInfo: null, downstreamFrequency: out var receivedFrequency));
            Assert.Equal(receivedFrequency, downlinkJoinAcceptMessage.Rx1?.Frequency);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Multiple_Joins_Are_Received_Should_Get_Twins_Once(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));

            var devAddr = (DevAddr?)null;
            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // Device twin will be queried
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties());
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            // Device twin will be updated
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, It.IsAny<DevNonce>()))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            // using factory to create mock of
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // 1st join request
            using var joinRequest1 =
                CreateWaitableRequest(simulatedDevice.CreateJoinRequest());
            messageProcessor.DispatchRequest(joinRequest1);

            await Task.Delay(100);

            // 2nd join request
            using var joinRequest2 =
                CreateWaitableRequest(simulatedDevice.CreateJoinRequest());
            messageProcessor.DispatchRequest(joinRequest2);

            await Task.WhenAll(joinRequest1.WaitCompleteAsync(), joinRequest2.WaitCompleteAsync());

            Assert.Equal(2, DownstreamMessageSender.DownlinkMessages.Count);
            Assert.NotNull(joinRequest1.ResponseDownlink);
            Assert.NotNull(joinRequest2.ResponseDownlink);

            // get twin only once called
            LoRaDeviceClient.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Once());

            // get device for join called x2
            LoRaDeviceApi.Verify(x => x.SearchAndLockForJoinAsync(ServerGatewayID, devEui, It.IsAny<DevNonce>()), Times.Exactly(2));

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }
    }
}
