// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    public class JoinSlowGetTwinTests : MessageProcessorTestBase
    {
        public JoinSlowGetTwinTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Join_Fails_Due_To_Timeout_Second_Try_Should_Reuse_Cached_Device_Twin(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequestPayload1 = simulatedDevice.CreateJoinRequest();

            var devAddr = (DevAddr?)null;
            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // Device twin will be queried twice, 1st time will take 7 seconds, 2nd time 0.1 second
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties());
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
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

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            await using var messageProcessor = TestMessageDispatcher.Create(
                memoryCache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // 1st join request
            // Should fail
            using var joinRequest1 = CreateWaitableRequest(joinRequestPayload1, constantElapsedTime: TimeSpan.FromSeconds(7));
            messageProcessor.DispatchRequest(joinRequest1);
            Assert.True(await joinRequest1.WaitCompleteAsync());
            Assert.True(joinRequest1.ProcessingFailed);
            Assert.Null(joinRequest1.ResponseDownlink);
            Assert.Equal(LoRaDeviceRequestFailedReason.ReceiveWindowMissed, joinRequest1.ProcessingFailedReason);

            // 2nd attempt
            var joinRequestPayload2 = simulatedDevice.CreateJoinRequest();

            // setup response to this device search
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequestPayload2.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            using var joinRequest2 = CreateWaitableRequest(joinRequestPayload2);
            messageProcessor.DispatchRequest(joinRequest2);
            Assert.True(await joinRequest2.WaitCompleteAsync());
            Assert.True(joinRequest2.ProcessingSucceeded);
            Assert.NotNull(joinRequest2.ResponseDownlink);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);
            var joinRequestDownlinkMessage = DownstreamMessageSender.DownlinkMessages[0];
            var joinAccept = new LoRaPayloadJoinAccept(joinRequestDownlinkMessage.Data, simulatedDevice.LoRaDevice.AppKey.Value);
            Assert.Equal(joinAccept.DevAddr.ToString(), afterJoinDevAddr);

            Assert.True(DeviceCache.TryGetByDevEui(devEui, out var loRaDevice));

            Assert.Equal(simulatedDevice.AppKey, loRaDevice.AppKey);
            Assert.Equal(simulatedDevice.AppEui, loRaDevice.AppEui);
            Assert.Equal(afterJoinAppSKey, loRaDevice.AppSKey);
            Assert.Equal(afterJoinNwkSKey, loRaDevice.NwkSKey);
            Assert.Equal(joinAccept.DevAddr, loRaDevice.DevAddr);
            if (deviceGatewayID == null)
                Assert.Null(loRaDevice.GatewayID);
            else
                Assert.Equal(deviceGatewayID, loRaDevice.GatewayID);

            // fcnt is restarted
            Assert.Equal(0U, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.FCntDown);
            Assert.False(loRaDevice.HasFrameCountChanges);

            // searching the device should happen twice
            LoRaDeviceApi.Verify(x => x.SearchAndLockForJoinAsync(ServerGatewayID, devEui, It.IsAny<DevNonce>()), Times.Exactly(2));

            // getting the device twin should happens once
            LoRaDeviceClient.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Once());

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }
    }
}
