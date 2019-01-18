//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using LoRaWan.NetworkServer.V2;
using LoRaWan.Test.Shared;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;


namespace LoRaWan.NetworkServer.Test
{
    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    public class MessageProcessor_End2End_NoDep_Join_Slow_Twin_Update_Tests : MessageProcessorTestBase
    {
        /// <summary>
        /// Verifies that if the update twin takes too long that no join accepts are sent
        /// </summary>
        /// <param name="deviceGatewayID"></param>
        /// <returns></returns>
        [Theory]
        [InlineData(MessageProcessorTestBase.ServerGatewayID)]
        [InlineData(null)]
        public async Task When_First_Join_Fails_Due_To_Slow_Twin_Update_Retry_Second_Attempt_Should_Succeed(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(1, gatewayID: deviceGatewayID));
            var joinRequest1 = simulatedDevice.CreateJoinRequest();
            var joinRequest2 = simulatedDevice.CreateJoinRequest();

            // Create Rxpk
            var joinRequestRxpk1 = joinRequest1.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).rxpk[0];
            var joinRequestRxpk2 = joinRequest2.SerializeUplink(simulatedDevice.LoRaDevice.AppKey).rxpk[0];


            var joinRequestDevNonce1 = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest1.DevNonce);
            var joinRequestDevNonce2 = LoRaTools.Utils.ConversionHelper.ByteArrayToString(joinRequest2.DevNonce);
            var devAddr = string.Empty;
            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var appEUI = simulatedDevice.LoRaDevice.AppEUI;

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            // Device twin will be queried
            var twin = new Twin();
            twin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            twin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            twin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            twin.Properties.Desired[TwinProperty.GatewayID] = deviceGatewayID;
            twin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            // Device twin will be updated
            string afterJoin1AppSKey = null;
            string afterJoin1NwkSKey = null;
            string afterJoin1DevAddr = null;
            string afterJoin2AppSKey = null;
            string afterJoin2NwkSKey = null;
            string afterJoin2DevAddr = null;
            var isFirstTwinUpdate = true;
            loRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .Callback<TwinCollection>((updatedTwin) =>
                {
                    if (isFirstTwinUpdate)
                    {
                        afterJoin1AppSKey = updatedTwin[TwinProperty.AppSKey];
                        afterJoin1NwkSKey = updatedTwin[TwinProperty.NwkSKey];
                        afterJoin1DevAddr = updatedTwin[TwinProperty.DevAddr];

                        Thread.Sleep(TimeSpan.FromSeconds(10));
                        isFirstTwinUpdate = false;
                    }
                    else
                    {
                        afterJoin2AppSKey = updatedTwin[TwinProperty.AppSKey];
                        afterJoin2NwkSKey = updatedTwin[TwinProperty.NwkSKey];
                        afterJoin2DevAddr = updatedTwin[TwinProperty.DevAddr];
                    }
                })
                .ReturnsAsync(true);

            // Lora device api will be search by devices with matching deveui,
            var loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            loRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, appEUI, joinRequestDevNonce1))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            loRaDeviceApi.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, appEUI, joinRequestDevNonce2))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "aabb").AsList()));

            // using factory to create mock of 
            var loRaDeviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, loRaDeviceApi.Object, loRaDeviceFactory);

            var frameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerConfiguration.GatewayID, loRaDeviceApi.Object);

            var messageProcessor = new LoRaWan.NetworkServer.V2.MessageProcessor(
                this.ServerConfiguration,
                deviceRegistry,
                frameCounterUpdateStrategyFactory,
                new LoRaPayloadDecoder()
                );

            var joinRequestTask1 = messageProcessor.ProcessMessageAsync(joinRequestRxpk1);

            await Task.Delay(TimeSpan.FromSeconds(7));

            var joinRequestTask2 = messageProcessor.ProcessMessageAsync(joinRequestRxpk2);

            await Task.WhenAll(joinRequestTask1, joinRequestTask2);
            Assert.Null(joinRequestTask1.Result);
            Assert.NotNull(joinRequestTask2.Result);

            Assert.Empty(deviceRegistry.InternalGetCachedDevicesForDevAddr(afterJoin1DevAddr));
            var devicesInDevAddr2 = deviceRegistry.InternalGetCachedDevicesForDevAddr(afterJoin2DevAddr);
            Assert.NotEmpty(devicesInDevAddr2);
            Assert.True(devicesInDevAddr2.TryGetValue(devEUI, out var loRaDevice));
            Assert.True(loRaDevice.IsOurDevice);
            Assert.Equal(afterJoin2DevAddr, loRaDevice.DevAddr);
            Assert.Equal(afterJoin2NwkSKey, loRaDevice.NwkSKey);
            Assert.Equal(afterJoin2AppSKey, loRaDevice.AppSKey);

            loRaDeviceClient.VerifyAll();
            loRaDeviceApi.VerifyAll();
        }
    }
}
