// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Devices that have keep alive set
    public class E2E_KeepAliveConnection_Tests : MessageProcessorTestBase
    {
        public int MaxWaitForDeviceConnectionInMs
        {
            get
            {
                if (Debugger.IsAttached)
                    return 30 * 1000;

                return 15 * 1000;
            }
        }

        public E2E_KeepAliveConnection_Tests()
        {
        }

        private async Task EnsureDisconnectedAsync(SemaphoreSlim disconnectedEvent, int? timeout = null)
        {
            var actualTimeout = timeout ?? this.MaxWaitForDeviceConnectionInMs;
            var totalWaitTime = 0;
            while (totalWaitTime < actualTimeout)
            {
                this.ConnectionManager.TryScanExpiredItems();
                if (await disconnectedEvent.WaitAsync(actualTimeout / 4))
                    break;

                totalWaitTime += actualTimeout / 4;
            }

            Assert.True(totalWaitTime < actualTimeout, "Did not disconnect device client");
        }

        [Fact]
        public async Task After_ClassA_Sends_Data_Should_Disconnect()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            simulatedDevice.FrmCntUp = 10;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // will check client connection
            this.LoRaDeviceClient.Setup(x => x.EnsureConnected())
                .Returns(true);

            // will disconnected client
            var disconnectedEvent = new SemaphoreSlim(0, 1);
            this.LoRaDeviceClient.Setup(x => x.Disconnect())
                .Callback(() => disconnectedEvent.Release())
                .Returns(true);

            var cachedDevice = this.CreateLoRaDevice(simulatedDevice);
            cachedDevice.KeepAliveTimeout = 3;

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(cachedDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello");
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            await this.EnsureDisconnectedAsync(disconnectedEvent);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task After_ClassA_Sends_Multiple_Data_Should_Disconnect()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            simulatedDevice.FrmCntUp = 10;

            var isDisconnected = false;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) =>
                {
                    Assert.False(isDisconnected);
                    loRaDeviceTelemetry = t;
                })
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .Callback(() =>
                {
                    Assert.False(isDisconnected);
                })
                .ReturnsAsync((Message)null);

            // will check client connection
            this.LoRaDeviceClient.Setup(x => x.EnsureConnected())
                .Returns(true);

            // will disconnected client
            var disconnectedEvent = new SemaphoreSlim(0, 1);
            this.LoRaDeviceClient.Setup(x => x.Disconnect())
                .Callback(() =>
                {
                    disconnectedEvent.Release();
                    isDisconnected = true;
                })
                .Returns(true);

            var cachedDevice = this.CreateLoRaDevice(simulatedDevice);
            cachedDevice.KeepAliveTimeout = 3;

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(cachedDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            foreach (var msg in Enumerable.Range(1, 3))
            {
                var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msg.ToString());
                var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
                var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
                messageDispatcher.DispatchRequest(request);
                Assert.True(await request.WaitCompleteAsync());
                Assert.True(request.ProcessingSucceeded);

                await Task.Delay(1500);
            }

            await this.EnsureDisconnectedAsync(disconnectedEvent);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task After_Disconnecting_Should_Reconnect()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            simulatedDevice.FrmCntUp = 10;

            var isDisconnected = false;

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) =>
                {
                    Assert.False(isDisconnected);
                    loRaDeviceTelemetry = t;
                })
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .Callback(() =>
                {
                    Assert.False(isDisconnected);
                })
                .ReturnsAsync((Message)null);

            // will check client connection
            this.LoRaDeviceClient.Setup(x => x.EnsureConnected())
                .Callback(() => isDisconnected = false)
                .Returns(true);

            // will disconnected client
            var disconnectedEvent = new SemaphoreSlim(0, 1);
            this.LoRaDeviceClient.Setup(x => x.Disconnect())
                .Callback(() =>
                {
                    disconnectedEvent.Release();
                    isDisconnected = true;
                })
                .Returns(true);

            var cachedDevice = this.CreateLoRaDevice(simulatedDevice);
            cachedDevice.KeepAliveTimeout = 3;

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(cachedDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message #1
            var request1 = new WaitableLoRaRequest(simulatedDevice.CreateUnconfirmedMessageUplink("1").Rxpk[0], this.PacketForwarder);
            messageDispatcher.DispatchRequest(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.True(request1.ProcessingSucceeded);

            await this.EnsureDisconnectedAsync(disconnectedEvent);

            // sends unconfirmed message #2
            var request2 = new WaitableLoRaRequest(simulatedDevice.CreateUnconfirmedMessageUplink("2").Rxpk[0], this.PacketForwarder);
            messageDispatcher.DispatchRequest(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.True(request2.ProcessingSucceeded);

            await this.EnsureDisconnectedAsync(disconnectedEvent);

            this.LoRaDeviceClient.Verify(x => x.Disconnect(), Times.Exactly(2));
            this.LoRaDeviceClient.Verify(x => x.EnsureConnected(), Times.Exactly(2));

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Loaded_Should_Disconnect_After_Sending_Data()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerGatewayID));

            // will search for the device by devAddr
            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(simulatedDevice.DevAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simulatedDevice.DevAddr, simulatedDevice.DevEUI, "ada").AsList()));

            // will read the device twins
            var twin = simulatedDevice.CreateABPTwin(desiredProperties: new Dictionary<string, object>
            {
                { TwinProperty.KeepAliveTimeout, 3 }
            });

            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(twin);

            // message will be sent
            LoRaDeviceTelemetry loRaDeviceTelemetry = null;
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) => loRaDeviceTelemetry = t)
                .ReturnsAsync(true);

            // C2D message will be checked
            this.LoRaDeviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .ReturnsAsync((Message)null);

            // will check client connection
            this.LoRaDeviceClient.Setup(x => x.EnsureConnected())
                .Returns(true);

            // will disconnected client
            var disconnectedEvent = new SemaphoreSlim(0, 1);
            this.LoRaDeviceClient.Setup(x => x.Disconnect())
                .Callback(() => disconnectedEvent.Release())
                .Returns(true);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage("hello");
            var rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey).Rxpk[0];
            var request = new WaitableLoRaRequest(rxpk, this.PacketForwarder);
            messageDispatcher.DispatchRequest(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            await this.EnsureDisconnectedAsync(disconnectedEvent, (int)TimeSpan.FromSeconds(Constants.MIN_KEEP_ALIVE_TIMEOUT * 2).TotalMilliseconds);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        [Fact]
        public async Task After_Sending_Class_C_Downstream_Should_Disconnect_Client()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: this.ServerConfiguration.GatewayID, deviceClassType: 'c'));
            var devEUI = simulatedDevice.DevEUI;

            // will disconnected client
            var disconnectedEvent = new SemaphoreSlim(0, 1);
            this.LoRaDeviceClient.Setup(x => x.Disconnect())
                .Callback(() =>
                {
                    disconnectedEvent.Release();
                })
                .Returns(true);

            // will check client connection
            this.LoRaDeviceClient.Setup(x => x.EnsureConnected())
                .Returns(true);

            // will save twin
            this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                .ReturnsAsync(true);

            var c2dToDeviceMessage = new ReceivedLoRaCloudToDeviceMessage()
            {
                Payload = "hello",
                DevEUI = devEUI,
                Fport = 10,
                MessageId = Guid.NewGuid().ToString(),
            };

            var cachedDevice = this.CreateLoRaDevice(simulatedDevice);
            cachedDevice.KeepAliveTimeout = 3;
            cachedDevice.LoRaRegion = LoRaRegionType.EU868;
            cachedDevice.InternalAcceptChanges();
            cachedDevice.SetFcntDown(cachedDevice.FCntDown + Constants.MAX_FCNT_UNSAVED_DELTA - 1);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewNonEmptyCache(cachedDevice), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var target = new DefaultClassCDevicesMessageSender(
                this.ServerConfiguration,
                deviceRegistry,
                this.PacketForwarder,
                this.FrameCounterUpdateStrategyProvider);

            Assert.True(await target.SendAsync(c2dToDeviceMessage));
            Assert.Single(this.PacketForwarder.DownlinkMessages);

            await this.EnsureDisconnectedAsync(disconnectedEvent);

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }
    }
}