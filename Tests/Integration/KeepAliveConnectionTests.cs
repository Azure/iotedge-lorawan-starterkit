// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Devices that have keep alive set
    public class KeepAliveConnectionTests : MessageProcessorTestBase
    {
        private readonly ITestOutputHelper testOutputHelper;

        public KeepAliveConnectionTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        public static int MaxWaitForDeviceConnectionInMs
        {
            get
            {
                if (Debugger.IsAttached)
                    return 30 * 1000;

                return 15 * 1000;
            }
        }

        private async Task EnsureDisconnectedAsync(SemaphoreSlim disconnectedEvent, int? timeout = null)
        {
            var actualTimeout = timeout ?? MaxWaitForDeviceConnectionInMs;
            var totalWaitTime = 0;
            while (totalWaitTime < actualTimeout)
            {
                ConnectionManager.TryScanExpiredItems();
                if (await disconnectedEvent.WaitAsync(actualTimeout / 4))
                    break;

                totalWaitTime += actualTimeout / 4;
            }

            Assert.True(totalWaitTime < actualTimeout, "Did not disconnect device client");
        }

        [Fact]
        public async Task After_ClassA_Sends_Data_Should_Disconnect()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1))
            {
                FrmCntUp = 10
            };

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // will check client connection
            LoRaDeviceClient.Setup(x => x.EnsureConnected())
                .Returns(true);

            // will disconnected client
            using var disconnectedEvent = new SemaphoreSlim(0, 1);
            LoRaDeviceClient.Setup(x => x.DisconnectAsync(It.IsAny<CancellationToken>()))
                .Callback(() => disconnectedEvent.Release())
                .Returns(Task.CompletedTask);

            var cachedDevice = CreateLoRaDevice(simulatedDevice);
            cachedDevice.KeepAliveTimeout = 3;

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(cachedDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            await using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello");
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            await EnsureDisconnectedAsync(disconnectedEvent);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task After_ClassA_Sends_Multiple_Data_Should_Disconnect()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1))
            {
                FrmCntUp = 10
            };

            var isDisconnected = false;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(isDisconnected ? throw new InvalidOperationException("Test setup requires that it may not be disconnected.") : true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync(isDisconnected ? throw new InvalidOperationException("Test setup requires that it may not be disconnected.") : (Message)null);

            // will check client connection
            LoRaDeviceClient.Setup(x => x.EnsureConnected())
                .Returns(true);

            // will disconnected client
            using var disconnectedEvent = new SemaphoreSlim(0, 1);
            LoRaDeviceClient.Setup(x => x.DisconnectAsync(It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    disconnectedEvent.Release();
                    isDisconnected = true;
                })
                .Returns(Task.CompletedTask);

            var cachedDevice = CreateLoRaDevice(simulatedDevice);
            cachedDevice.KeepAliveTimeout = 3;

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(cachedDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            await using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            foreach (var msg in Enumerable.Range(1, 3))
            {
                var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msg.ToString(CultureInfo.InvariantCulture));

                using var request = CreateWaitableRequest(unconfirmedMessagePayload);
                messageDispatcher.DispatchRequest(request);
                Assert.True(await request.WaitCompleteAsync());
                Assert.True(request.ProcessingSucceeded);

                await Task.Delay(1500);
            }

            await EnsureDisconnectedAsync(disconnectedEvent);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task After_Disconnecting_Should_Reconnect()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1))
            {
                FrmCntUp = 10
            };

            var isDisconnected = false;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(isDisconnected ? throw new InvalidOperationException("Test setup requires that it may not be disconnected.") : true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync(isDisconnected ? throw new InvalidOperationException("Test setup requires that it may not be disconnected.") : (Message)null);

            // will check client connection
            LoRaDeviceClient.Setup(x => x.EnsureConnected())
                .Callback(() => isDisconnected = false)
                .Returns(true);

            // will disconnected client
            using var disconnectedEvent = new SemaphoreSlim(0, 1);
            LoRaDeviceClient.Setup(x => x.DisconnectAsync(It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    disconnectedEvent.Release();
                    isDisconnected = true;
                })
                .Returns(Task.CompletedTask);

            var cachedDevice = CreateLoRaDevice(simulatedDevice);
            cachedDevice.KeepAliveTimeout = 3;

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(cachedDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            await using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message #1
            using var request1 = CreateWaitableRequest(simulatedDevice.CreateUnconfirmedDataUpMessage("1"));
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.True(request1.ProcessingSucceeded);

            await EnsureDisconnectedAsync(disconnectedEvent);
            LoRaDeviceClient.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Exactly(1));
            LoRaDeviceClient.Verify(x => x.EnsureConnected(), Times.Exactly(2));

            // sends unconfirmed message #2
            using var request2 = CreateWaitableRequest(simulatedDevice.CreateUnconfirmedDataUpMessage("2"));
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.True(request2.ProcessingSucceeded);

            await EnsureDisconnectedAsync(disconnectedEvent);

            LoRaDeviceClient.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Exactly(2));
            LoRaDeviceClient.Verify(x => x.EnsureConnected(), Times.Exactly(2 * /* send + receive */ 2));
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Loaded_Should_Disconnect_After_Sending_Data()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));

            // will search for the device by devAddr
            LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(simulatedDevice.DevAddr.Value))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simulatedDevice.DevAddr, simulatedDevice.DevEUI, "ada").AsList()));

            // will read the device twins
            var twin = LoRaDeviceTwin.Create(simulatedDevice.LoRaDevice.GetAbpDesiredTwinProperties() with { KeepAliveTimeout = TimeSpan.FromSeconds(3) },
                                             simulatedDevice.GetAbpReportedTwinProperties());

            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(twin);

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // will check client connection
            LoRaDeviceClient.Setup(x => x.EnsureConnected())
                .Returns(true);

            // will disconnected client
            using var disconnectedEvent = new SemaphoreSlim(0, 1);
            LoRaDeviceClient.Setup(x => x.DisconnectAsync(It.IsAny<CancellationToken>()))
                .Callback(() => disconnectedEvent.Release())
                .Returns(Task.CompletedTask);

            using var cache = NewMemoryCache();
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, DeviceCache);

            await using var messageDispatcher = TestMessageDispatcher.Create(
                cache,
                ServerConfiguration,
                deviceRegistry,
                FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello");
            using var request = CreateWaitableRequest(unconfirmedMessagePayload);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            await EnsureDisconnectedAsync(disconnectedEvent, (int)TimeSpan.FromSeconds(Constants.MinKeepAliveTimeout * 2).TotalMilliseconds);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task After_Sending_Class_C_Downstream_Should_Disconnect_Client()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1,
                                                                                     gatewayID: ServerConfiguration.GatewayID,
                                                                                     deviceClassType: LoRaDeviceClassType.C));
            var devEui = simulatedDevice.DevEUI;

            // will disconnected client
            using var disconnectedEvent = new SemaphoreSlim(0, 1);
            LoRaDeviceClient.Setup(x => x.DisconnectAsync(It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    disconnectedEvent.Release();
                })
                .Returns(Task.CompletedTask);

            // will check client connection
            LoRaDeviceClient.Setup(x => x.EnsureConnected())
                .Returns(true);

            // will save twin
            LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var c2dToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "hello",
                DevEUI = devEui,
                Fport = FramePorts.App10,
                MessageId = Guid.NewGuid().ToString(),
            };

            var cachedDevice = CreateLoRaDevice(simulatedDevice);
            cachedDevice.KeepAliveTimeout = 3;
            cachedDevice.LoRaRegion = LoRaRegionType.EU868;
            cachedDevice.InternalAcceptChanges();
            cachedDevice.SetFcntDown(cachedDevice.FCntDown + Constants.MaxFcntUnsavedDelta - 1);
            cachedDevice.SetLastProcessingStationEui(new StationEui(ulong.MaxValue));

            using var cache = EmptyMemoryCache();
            await using var loraDeviceCache = CreateDeviceCache(cachedDevice);
            await using var deviceRegistry = new LoRaDeviceRegistry(ServerConfiguration, cache, LoRaDeviceApi.Object, LoRaDeviceFactory, loraDeviceCache);

            var target = new DefaultClassCDevicesMessageSender(
                ServerConfiguration,
                deviceRegistry,
                DownstreamMessageSender,
                FrameCounterUpdateStrategyProvider,
                new TestOutputLogger<DefaultClassCDevicesMessageSender>(testOutputHelper),
                TestMeter.Instance);

            Assert.True(await target.SendAsync(c2dToDeviceMessage));
            Assert.Single(DownstreamMessageSender.DownlinkMessages);

            await EnsureDisconnectedAsync(disconnectedEvent);

            LoRaDeviceClient.VerifyAll();
            LoRaDeviceApi.VerifyAll();
        }
    }
}
