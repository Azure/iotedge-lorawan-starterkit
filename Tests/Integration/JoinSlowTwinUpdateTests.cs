// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    public class JoinSlowTwinUpdateTests : MessageProcessorTestBase
    {
        public JoinSlowTwinUpdateTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

        /// <summary>
        /// Verifies that if the update twin takes too long that no join accepts are sent.
        /// </summary>
        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_First_Join_Fails_Due_To_Slow_Twin_Update_Retry_Second_Attempt_Should_Succeed(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequestPayload1 = simulatedDevice.CreateJoinRequest();
            var joinRequestPayload2 = simulatedDevice.CreateJoinRequest();

            var devAddr = (DevAddr?)null;
            var devEui = simulatedDevice.LoRaDevice.DevEui;

            // Device twin will be queried
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetOtaaDesiredTwinProperties());
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            // Device twin will be updated
            AppSessionKey? afterJoin2AppSKey = null;
            NetworkSessionKey? afterJoin2NwkSKey = null;
            DevAddr? afterJoin2DevAddr = null;

            var mockSequence = new MockSequence();
            LoRaDeviceClient.InSequence(mockSequence)
                            .Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                            .Returns<TwinCollection, CancellationToken>(async (_, token) =>
                            {
                                try
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(20), token);
                                    Assert.True(false, "Token timeout expected");
                                }
                                catch (OperationCanceledException) { }
                                return false;
                            });
            LoRaDeviceClient.InSequence(mockSequence)
                            .Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                            .Returns<TwinCollection, CancellationToken>((updatedTwin, token) =>
                            {
                                afterJoin2AppSKey = updatedTwin.SafeRead<AppSessionKey>(TwinProperty.AppSKey);
                                afterJoin2NwkSKey = updatedTwin.SafeRead<NetworkSessionKey>(TwinProperty.NwkSKey);
                                afterJoin2DevAddr = updatedTwin.SafeRead<DevAddr>(TwinProperty.DevAddr);
                                return Task.FromResult(true);
                            });

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequestPayload1.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEui, joinRequestPayload2.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEui, "aabb").AsList()));

            using var cache = NewMemoryCache();
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            await using var messageProcessor = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            using var joinRequest1 = CreateWaitableRequest(joinRequestPayload1, useRealTimer: true);
            messageProcessor.DispatchRequest(joinRequest1);
            await Task.Delay(TimeSpan.FromSeconds(7));

            using var joinRequest2 = CreateWaitableRequest(joinRequestPayload2, useRealTimer: true);
            messageProcessor.DispatchRequest(joinRequest2);

            await Task.WhenAll(joinRequest1.WaitCompleteAsync(), joinRequest2.WaitCompleteAsync());
            Assert.True(joinRequest1.ProcessingFailed);
            Assert.Null(joinRequest1.ResponseDownlink);
            Assert.Equal(LoRaDeviceRequestFailedReason.IoTHubProblem, joinRequest1.ProcessingFailedReason);
            Assert.True(joinRequest2.ProcessingSucceeded);
            Assert.NotNull(joinRequest2.ResponseDownlink);
            Assert.Single(DownstreamMessageSender.DownlinkMessages);

            Assert.True(DeviceCache.TryGetByDevEui(devEui, out var loRaDevice));
            Assert.True(loRaDevice.IsOurDevice);
            Assert.Equal(afterJoin2DevAddr, loRaDevice.DevAddr);
            Assert.Equal(afterJoin2NwkSKey, loRaDevice.NwkSKey);
            Assert.Equal(afterJoin2AppSKey, loRaDevice.AppSKey);

            // get twin should happen only once
            LoRaDeviceClient.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Once());
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }
    }
}
