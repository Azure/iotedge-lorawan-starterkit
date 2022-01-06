// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    public class JoinSlowTwinUpdateTests : MessageProcessorTestBase
    {
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

            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            // Device twin will be updated
            string afterJoin2AppSKey = null;
            string afterJoin2NwkSKey = null;
            string afterJoin2DevAddr = null;

            var invocations = 0;
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .Returns<TwinCollection, CancellationToken>(async (updatedTwin, token) =>
                {
                    if (invocations == 0)
                    {
                        invocations++;
                        await Task.Delay(7_000, CancellationToken.None);
                        return token.IsCancellationRequested
                            ? false
                            : throw new InvalidOperationException("First request should have been canceled after timeout.");
                    }
                    else
                    {
                        afterJoin2AppSKey = updatedTwin[TwinProperty.AppSKey];
                        afterJoin2NwkSKey = updatedTwin[TwinProperty.NwkSKey];
                        afterJoin2DevAddr = updatedTwin[TwinProperty.DevAddr];
                        return true;
                    }
                });

            // Lora device api will be search by devices with matching deveui,
            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, joinRequestPayload1.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, joinRequestPayload2.DevNonce))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            using var cache = NewMemoryCache();
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            using var messageProcessor = new MessageDispatcher(
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
            Assert.Single(PacketForwarder.DownlinkMessages);

            Assert.True(DeviceCache.TryGetByDevEui(devEUI, out var loRaDevice));
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
