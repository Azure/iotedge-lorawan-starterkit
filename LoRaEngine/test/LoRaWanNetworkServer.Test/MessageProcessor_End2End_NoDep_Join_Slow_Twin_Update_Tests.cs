// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    public class MessageProcessor_End2End_NoDep_Join_Slow_Twin_Update_Tests : MessageProcessorTestBase
    {
        /// <summary>
        /// Verifies that if the update twin takes too long that no join accepts are sent
        /// </summary>
        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_First_Join_Fails_Due_To_Slow_Twin_Update_Retry_Second_Attempt_Should_Succeed(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequestPayload1 = simulatedDevice.CreateJoinRequest();
            var joinRequestPayload2 = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var joinRequestRxpk1 = joinRequestPayload1.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).Rxpk[0];
            var joinRequestRxpk2 = joinRequestPayload2.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).Rxpk[0];

            var joinRequestDevNonce1 = joinRequestPayload1.GetDevNonceAsString();
            var joinRequestDevNonce2 = joinRequestPayload2.GetDevNonceAsString();
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            // Device twin will be updated
            string afterJoin1AppSKey = null;
            string afterJoin1NwkSKey = null;
            string afterJoin1DevAddr = null;
            string afterJoin2AppSKey = null;
            string afterJoin2NwkSKey = null;
            string afterJoin2DevAddr = null;

            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true, TimeSpan.FromSeconds(10))
                .Callback<TwinCollection>((updatedTwin) =>
                {
                    afterJoin1AppSKey = updatedTwin[TwinProperty.AppSKey];
                    afterJoin1NwkSKey = updatedTwin[TwinProperty.NwkSKey];
                    afterJoin1DevAddr = updatedTwin[TwinProperty.DevAddr];

                    // update setup for no delay
                    this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                        .ReturnsAsync(true)
                        .Callback<TwinCollection>((updatedTwin2) =>
                        {
                            afterJoin2AppSKey = updatedTwin2[TwinProperty.AppSKey];
                            afterJoin2NwkSKey = updatedTwin2[TwinProperty.NwkSKey];
                            afterJoin2DevAddr = updatedTwin2[TwinProperty.DevAddr];
                        });
                });

            // Lora device api will be search by devices with matching deveui,
            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, joinRequestDevNonce1))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            this.LoRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(this.ServerConfiguration.GatewayID, devEUI, appEUI, joinRequestDevNonce2))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageProcessor = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var joinRequest1 = this.CreateWaitableRequest(joinRequestRxpk1);
            messageProcessor.DispatchRequest(joinRequest1);
            await Task.Delay(TimeSpan.FromSeconds(7));

            var joinRequest2 = this.CreateWaitableRequest(joinRequestRxpk2);
            messageProcessor.DispatchRequest(joinRequest2);

            await Task.WhenAll(joinRequest1.WaitCompleteAsync(), joinRequest2.WaitCompleteAsync());
            Assert.True(joinRequest1.ProcessingFailed);
            Assert.Null(joinRequest1.ResponseDownlink);
            Assert.True(joinRequest2.ProcessingSucceeded);
            Assert.NotNull(joinRequest2.ResponseDownlink);
            Assert.Single(this.PacketForwarder.DownlinkMessages);

            Assert.Empty(deviceRegistry.InternalGetCachedDevicesForDevAddr(afterJoin1DevAddr));
            var devicesInDevAddr2 = deviceRegistry.InternalGetCachedDevicesForDevAddr(afterJoin2DevAddr);
            Assert.NotEmpty(devicesInDevAddr2);
            Assert.True(devicesInDevAddr2.TryGetValue(devEUI, out var loRaDevice));
            Assert.True(loRaDevice.IsOurDevice);
            Assert.Equal(afterJoin2DevAddr, loRaDevice.DevAddr);
            Assert.Equal(afterJoin2NwkSKey, loRaDevice.NwkSKey);
            Assert.Equal(afterJoin2AppSKey, loRaDevice.AppSKey);

            // get twin should happen only once
            this.LoRaDeviceClient.Verify(x => x.GetTwinAsync(), Times.Once());
            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }
    }
}
