// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Multi gateway specifc scenarios
    public class MultiGatewayTests : MessageProcessorTestBase
    {
        [Theory]
        [InlineData(0, 0, 1)]
        public async Task When_Fcnt_Down_Fails_Should_Stop_And_Not_Update_Device_Twin(uint initialFcntDown, uint initialFcntUp, uint payloadFcnt)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: null))
            {
                FrmCntDown = initialFcntDown,
                FrmCntUp = initialFcntUp
            };

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            LoRaDeviceApi
                .Setup(x => x.ExecuteFunctionBundlerAsync(devEUI, It.IsAny<FunctionBundlerRequest>()))
                .ReturnsAsync(() =>
                    new FunctionBundlerResult
                    {
                        AdrResult = new LoRaTools.ADR.LoRaADRResult { CanConfirmToDevice = true, FCntDown = 0 },
                        NextFCntDown = 0
                    });

            LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(devEUI, It.IsAny<uint>(), It.IsNotNull<string>()))
                    .ReturnsAsync(true);

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var device = CreateLoRaDevice(simulatedDevice);

            DeviceCache.Register(device);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            using var messageDispatcher = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage("hello", fcnt: payloadFcnt);
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            // verify that the device in device registry has correct properties and frame counters
            Assert.True(DeviceCache.TryGetForPayload(request.Payload, out var loRaDevice));
            Assert.Equal(devAddr, loRaDevice.DevAddr);
            Assert.Equal(devEUI, loRaDevice.DevEUI);
            Assert.True(loRaDevice.IsABP);
            Assert.Equal(initialFcntUp, loRaDevice.FCntUp);
            Assert.Equal(initialFcntDown, loRaDevice.FCntDown);
            Assert.False(loRaDevice.HasFrameCountChanges);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Theory]
        [InlineData(null, 0U, null, null)]
        [InlineData(null, 0U, 1U, 1U)]
        [InlineData(null, 0U, 100U, 20U)]
        [InlineData(null, 1U, null, null)]
        [InlineData(null, 1U, 1U, 1U)]
        [InlineData(null, 1U, 100U, 20U)]
        public async Task ABP_New_Loaded_Device_With_Fcnt_1_Or_0_Should_Reset_Fcnt_And_Send_To_IotHub(
            string twinGatewayID,
            uint payloadFcntUp,
            uint? deviceTwinFcntUp,
            uint? deviceTwinFcntDown)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: null));

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // twin will be loaded
            var initialTwin = new Twin();
            initialTwin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            initialTwin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            initialTwin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            initialTwin.Properties.Desired[TwinProperty.NwkSKey] = simulatedDevice.LoRaDevice.NwkSKey;
            initialTwin.Properties.Desired[TwinProperty.AppSKey] = simulatedDevice.LoRaDevice.AppSKey;
            initialTwin.Properties.Desired[TwinProperty.DevAddr] = devAddr;
            if (twinGatewayID != null)
                initialTwin.Properties.Desired[TwinProperty.GatewayID] = twinGatewayID;
            initialTwin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            if (deviceTwinFcntDown.HasValue)
                initialTwin.Properties.Reported[TwinProperty.FCntDown] = deviceTwinFcntDown.Value;
            if (deviceTwinFcntUp.HasValue)
                initialTwin.Properties.Reported[TwinProperty.FCntUp] = deviceTwinFcntUp.Value;

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(initialTwin);

            // twin will be updated with new fcnt
            int? fcntUpSavedInTwin = null;
            int? fcntDownSavedInTwin = null;

            var shouldSaveTwin = (deviceTwinFcntDown ?? 0) != 0 || (deviceTwinFcntUp ?? 0) != 0;
            if (shouldSaveTwin)
            {
                LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                    .Callback<TwinCollection>((t) =>
                    {
                        fcntUpSavedInTwin = (int)t[TwinProperty.FCntUp];
                        fcntDownSavedInTwin = (int)t[TwinProperty.FCntDown];
                    })
                    .ReturnsAsync(true);
            }

            LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(devEUI, It.IsAny<uint>(), It.IsNotNull<string>()))
                    .ReturnsAsync(true);

            // device api will be searched for payload
            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "abc").AsList()));

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, memoryCache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            // Send to message processor
            using var messageDispatcher = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello", fcnt: payloadFcntUp);
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.Null(request.ResponseDownlink);

            // Ensure that a telemetry was sent
            Assert.NotNull(loRaDeviceTelemetry);

            // Ensure that the device twins were saved
            if (shouldSaveTwin)
            {
                Assert.NotNull(fcntDownSavedInTwin);
                Assert.NotNull(fcntUpSavedInTwin);
                Assert.Equal(0, fcntDownSavedInTwin.Value);
                Assert.Equal(0, fcntUpSavedInTwin.Value);
            }

            // verify that the device in device registry has correct properties and frame counters
            Assert.True(DeviceCache.TryGetForPayload(request.Payload, out var loRaDevice));
            Assert.Equal(devAddr, loRaDevice.DevAddr);
            Assert.Equal(devEUI, loRaDevice.DevEUI);
            Assert.True(loRaDevice.IsABP);
            Assert.Equal(payloadFcntUp, loRaDevice.FCntUp);
            Assert.Equal(0U, loRaDevice.FCntDown);
            if (payloadFcntUp == 0)
                Assert.False(loRaDevice.HasFrameCountChanges); // no changes
            else
                Assert.True(loRaDevice.HasFrameCountChanges); // should have changes!

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        /// <summary>
        /// If cannot get a fcntdown from api should drop the c2d message.
        /// </summary>
        [Fact]
        public async Task When_Getting_C2D_Message_Fails_To_Resolve_Fcnt_Down_Should_Abandon_Message_And_Return_Null()
        {
            const uint initialFcntDown = 5;
            const uint initialFcntUp = 21;
            const uint payloadFcnt = 23;

            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: null))
            {
                FrmCntUp = initialFcntUp,
                FrmCntDown = initialFcntDown
            };

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // message will be sent
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true);

            var c2dMessage = new ReceivedLoRaCloudToDeviceMessage() { Fport = (FramePort)1 };
            using var cloudToDeviceMessage = c2dMessage.CreateMessage();
            // C2D message will be retrieved
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync(cloudToDeviceMessage);

            // C2D message will not be abandoned

            // getting the fcnt down will return 0!
            LoRaDeviceApi.Setup(x => x.NextFCntDownAsync(devEUI, initialFcntDown, payloadFcnt, ServerConfiguration.GatewayID))
                 .ReturnsAsync((ushort)0);

            var cachedDevice = CreateLoRaDevice(simulatedDevice);
            using var cache = EmptyMemoryCache();
            using var loraDeviceCache = CreateDeviceCache(cachedDevice);
            using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            // Send to message processor
            using var messageDispatcher = new MessageDispatcher(
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello", fcnt: payloadFcnt);
            using var unconfirmedRequest = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(unconfirmedRequest);
            Assert.True(await unconfirmedRequest.WaitCompleteAsync());
            Assert.Null(unconfirmedRequest.ResponseDownlink);

            Assert.True(loraDeviceCache.TryGetForPayload(unconfirmedRequest.Payload, out var loRaDevice));
            // fcnt down did not change
            Assert.Equal(initialFcntDown, loRaDevice.FCntDown);

            // fcnt up changed
            Assert.Equal(unconfirmedMessagePayload.GetFcnt(), loRaDevice.FCntUp);

            LoRaDeviceClient.Verify(x => x.ReceiveAsync(It.IsAny<TimeSpan>()), Times.Once());
            LoRaDeviceClient.Verify(x => x.AbandonAsync(It.IsAny<Message>()), Times.Once());

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }
    }
}
