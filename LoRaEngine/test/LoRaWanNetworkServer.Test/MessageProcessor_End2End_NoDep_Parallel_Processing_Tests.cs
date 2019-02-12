﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    // End to end tests without external dependencies (IoT Hub, Service Facade Function)
    // Parallel message processing
    public class MessageProcessor_End2End_NoDep_Parallel_Processing_Tests : MessageProcessorTestBase
    {
        private readonly TestPacketForwarder packetForwarder;

        public MessageProcessor_End2End_NoDep_Parallel_Processing_Tests()
        {
            this.packetForwarder = new TestPacketForwarder();
        }

        public static IEnumerable<object[]> Multiple_ABP_Messages()
        {
            yield return new object[]
            {
                    new ParallelTestConfiguration()
                    {
                        GatewayID = ServerGatewayID,
                        BetweenMessageDuration = 1000,
                        SearchByDevAddrDuration = 100,
                        SendEventDuration = 100,
                        ReceiveEventDuration = 100,
                        UpdateTwinDuration = 100,
                        LoadTwinDuration = 100,
                    }
            };

            // Slow first calls
            yield return new object[]
            {
                    new ParallelTestConfiguration()
                    {
                        GatewayID = ServerGatewayID,
                        BetweenMessageDuration = 1000,
                        SearchByDevAddrDuration = new int[] { 1000, 100 },
                        SendEventDuration = new int[] { 1000, 100 },
                        ReceiveEventDuration = 400,
                        UpdateTwinDuration = new int[] { 1000, 100 },
                        LoadTwinDuration = new int[] { 1000, 100 },
                    }
            };

            // Very slow first calls
            yield return new object[]
            {
                    new ParallelTestConfiguration()
                    {
                        GatewayID = ServerGatewayID,
                        BetweenMessageDuration = 1000,
                        SearchByDevAddrDuration = new int[] { 5000, 100 },
                        SendEventDuration = new int[] { 1000, 100 },
                        ReceiveEventDuration = 400,
                        UpdateTwinDuration = new int[] { 5000, 100 },
                        LoadTwinDuration = new int[] { 5000, 100 },
                    }
            };
        }

        [Theory]
        [MemberData(nameof(Multiple_ABP_Messages))]
        public async Task ABP_Load_And_Receiving_Multiple_Unconfirmed_Should_Send_All_ToHub(ParallelTestConfiguration parallelTestConfiguration)
        {
            Console.WriteLine("---");
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: null));

            var devEUI = simulatedDevice.LoRaDevice.DeviceID;
            var devAddr = simulatedDevice.LoRaDevice.DevAddr;

            // message will be sent
            var sentTelemetry = new List<LoRaDeviceTelemetry>();
            this.LoRaDeviceClient.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Returns<LoRaDeviceTelemetry, Dictionary<string, string>>((t, _) =>
                {
                    sentTelemetry.Add(t);
                    var duration = parallelTestConfiguration.SendEventDuration.Next();
                    Console.WriteLine($"{nameof(this.LoRaDeviceClient.Object.SendEventAsync)} sleeping for {duration}");
                    return Task.Delay(duration)
                        .ContinueWith((a) => true);
                });

            // twin will be loaded
            var initialTwin = new Twin();
            initialTwin.Properties.Desired[TwinProperty.DevEUI] = devEUI;
            initialTwin.Properties.Desired[TwinProperty.AppEUI] = simulatedDevice.LoRaDevice.AppEUI;
            initialTwin.Properties.Desired[TwinProperty.AppKey] = simulatedDevice.LoRaDevice.AppKey;
            initialTwin.Properties.Desired[TwinProperty.NwkSKey] = simulatedDevice.LoRaDevice.NwkSKey;
            initialTwin.Properties.Desired[TwinProperty.AppSKey] = simulatedDevice.LoRaDevice.AppSKey;
            initialTwin.Properties.Desired[TwinProperty.DevAddr] = devAddr;
            if (parallelTestConfiguration.GatewayID != null)
                initialTwin.Properties.Desired[TwinProperty.GatewayID] = parallelTestConfiguration.GatewayID;
            initialTwin.Properties.Desired[TwinProperty.SensorDecoder] = simulatedDevice.LoRaDevice.SensorDecoder;
            if (parallelTestConfiguration.DeviceTwinFcntDown.HasValue)
                initialTwin.Properties.Reported[TwinProperty.FCntDown] = parallelTestConfiguration.DeviceTwinFcntDown.Value;
            if (parallelTestConfiguration.DeviceTwinFcntUp.HasValue)
                initialTwin.Properties.Reported[TwinProperty.FCntUp] = parallelTestConfiguration.DeviceTwinFcntUp.Value;

            this.LoRaDeviceClient.Setup(x => x.GetTwinAsync())
                .Returns(() =>
                {
                    var duration = parallelTestConfiguration.LoadTwinDuration.Next();
                    Console.WriteLine($"{nameof(this.LoRaDeviceClient.Object.GetTwinAsync)} sleeping for {duration}");
                    return Task.Delay(duration)
                        .ContinueWith(_ => initialTwin);
                });

            // twin will be updated with new fcnt
            int? fcntUpSavedInTwin = null;
            int? fcntDownSavedInTwin = null;

            var shouldSaveTwin = (parallelTestConfiguration.DeviceTwinFcntDown ?? 0) != 0 || (parallelTestConfiguration.DeviceTwinFcntUp ?? 0) != 0;
            if (shouldSaveTwin)
            {
                this.LoRaDeviceClient.Setup(x => x.UpdateReportedPropertiesAsync(It.IsNotNull<TwinCollection>()))
                    .Returns<TwinCollection>((t) =>
                    {
                        fcntUpSavedInTwin = (int)t[TwinProperty.FCntUp];
                        fcntDownSavedInTwin = (int)t[TwinProperty.FCntDown];
                        var duration = parallelTestConfiguration.UpdateTwinDuration.Next();
                        Console.WriteLine($"{nameof(this.LoRaDeviceClient.Object.UpdateReportedPropertiesAsync)} sleeping for {duration}");
                        return Task.Delay(duration)
                            .ContinueWith((a) => true);
                    });
            }

            // multi gateway will reset the fcnt
            if (shouldSaveTwin)
            {
                this.LoRaDeviceApi.Setup(x => x.ABPFcntCacheResetAsync(devEUI))
                    .Returns(() =>
                    {
                        var duration = parallelTestConfiguration.DeviceApiResetFcntDuration.Next();
                        Console.WriteLine($"{nameof(this.LoRaDeviceApi.Object.ABPFcntCacheResetAsync)} sleeping for {duration}");
                        return Task.Delay(duration)
                            .ContinueWith((a) => true);
                    });
            }

            // device api will be searched for payload
            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .Returns(() =>
                {
                    var duration = parallelTestConfiguration.SearchByDevAddrDuration.Next();
                    Console.WriteLine($"{nameof(this.LoRaDeviceApi.Object.SearchByDevAddrAsync)} sleeping for {duration}");
                    return Task.Delay(duration)
                        .ContinueWith((a) => new SearchDevicesResult(new IoTHubDeviceInfo(devAddr, devEUI, "abc").AsList()));
                });

            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, memoryCache, this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            // Send to message processor
            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            // sends unconfirmed message
            var unconfirmedMessage1 = simulatedDevice.CreateUnconfirmedMessageUplink("1", fcnt: 1).Rxpk[0];
            var unconfirmedMessage2 = simulatedDevice.CreateUnconfirmedMessageUplink("2", fcnt: 2).Rxpk[0];
            var unconfirmedMessage3 = simulatedDevice.CreateUnconfirmedMessageUplink("3", fcnt: 3).Rxpk[0];

            var packetForwarder = new TestPacketForwarder();

            var req1 = new WaitableLoRaRequest(unconfirmedMessage1, this.packetForwarder);
            messageDispatcher.DispatchRequest(req1);
            await Task.Delay(parallelTestConfiguration.BetweenMessageDuration.Next());

            var req2 = new WaitableLoRaRequest(unconfirmedMessage2, this.packetForwarder);
            messageDispatcher.DispatchRequest(req2);
            await Task.Delay(parallelTestConfiguration.BetweenMessageDuration.Next());

            var req3 = new WaitableLoRaRequest(unconfirmedMessage3, this.packetForwarder);
            messageDispatcher.DispatchRequest(req3);
            await Task.Delay(parallelTestConfiguration.BetweenMessageDuration.Next());

            await Task.WhenAll(req1.WaitCompleteAsync(), req2.WaitCompleteAsync(), req3.WaitCompleteAsync());

            Assert.Null(req1.ResponseDownlink);
            Assert.Null(req2.ResponseDownlink);
            Assert.Null(req3.ResponseDownlink);

            this.LoRaDeviceClient.Verify(x => x.GetTwinAsync(), Times.Exactly(1));
            this.LoRaDeviceClient.Verify(x => x.UpdateReportedPropertiesAsync(It.IsAny<TwinCollection>()), Times.Exactly(1));
            // loRaDeviceClient.Verify(x => x.ReceiveAsync(It.IsAny<TimeSpan>()), Times.Exactly(1));
            this.LoRaDeviceApi.Verify(x => x.SearchByDevAddrAsync(devAddr), Times.Once);

            // Ensure that all telemetry was sent
            Assert.Equal(3, sentTelemetry.Count);

            // Ensure data was sent in order
            Assert.Equal(1, sentTelemetry[0].Fcnt);
            Assert.Equal(2, sentTelemetry[1].Fcnt);
            Assert.Equal(3, sentTelemetry[2].Fcnt);

            // Ensure that the device twins were saved
            if (shouldSaveTwin)
            {
                Assert.NotNull(fcntDownSavedInTwin);
                Assert.NotNull(fcntUpSavedInTwin);
                Assert.Equal(0, fcntDownSavedInTwin.Value);
                Assert.Equal(0, fcntUpSavedInTwin.Value);
            }

            // verify that the device in device registry has correct properties and frame counters
            var devicesForDevAddr = deviceRegistry.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.Single(devicesForDevAddr);
            Assert.True(devicesForDevAddr.TryGetValue(devEUI, out var loRaDevice));
            Assert.Equal(devAddr, loRaDevice.DevAddr);
            Assert.Equal(devEUI, loRaDevice.DevEUI);
            Assert.True(loRaDevice.IsABP);
            Assert.Equal(3, loRaDevice.FCntUp);
            Assert.Equal(0, loRaDevice.FCntDown);
            Assert.True(loRaDevice.HasFrameCountChanges); // should have changes!

            this.LoRaDeviceClient.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }

        async Task<List<WaitableLoRaRequest>> SendMessages(SimulatedDevice device, MessageDispatcher dispatcher, int payloadInitialFcnt, int delayBetweenMessages = 1000)
        {
            var requests = new List<WaitableLoRaRequest>();
            for (var i = 0; i < 5; ++i)
            {
                var rxpk = device.CreateUnconfirmedMessageUplink((i + 1).ToString(), fcnt: payloadInitialFcnt + i).Rxpk[0];
                var req = this.CreateWaitableRequest(rxpk);
                dispatcher.DispatchRequest(req);
                requests.Add(req);

                await Task.Delay(delayBetweenMessages);
            }

            return requests;
        }

        // 4 devices
        // 2 sharing the same devAddr
        // Each sending 5 messages in 1 second time
        // Search takes 200ms
        // Getting twin takes 300ms
        [Theory]
        [InlineData(200, 300, 100, 20, 1000)]
        [InlineData(200, 300, 100, 20, 10)]
        public async Task When_Multiple_Devices_Send_Telemetry_Queue_Sends_Messages_To_IoTHub(
            int searchDelay,
            int getTwinDelay,
            int sendMessageDelay,
            int receiveDelay,
            int delayBetweenMessages)
        {
            const int payloadInitialFcnt = 2;

            var device1 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var device1Twin = TestUtils.CreateABPTwin(device1);
            var device2 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(2));
            device2.DevAddr = device1.DevAddr;
            var device2Twin = TestUtils.CreateABPTwin(device2);
            var device3 = new SimulatedDevice(TestDeviceInfo.CreateOTAADevice(3));
            device3.SetupJoin("00000000000000000000000000000088", "00000000000000000000000000000088", "02000088");
            var device3Twin = TestUtils.CreateOTAATwin(device3);
            var device4 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(4));
            var device4Twin = TestUtils.CreateABPTwin(device4);

            var device1And2Result = new IoTHubDeviceInfo[]
            {
                new IoTHubDeviceInfo(device1.DevAddr, device1.DevEUI, "1"),
                new IoTHubDeviceInfo(device2.DevAddr, device2.DevEUI, "2"),
            };

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(device1.DevAddr))
                .Returns(Task.Delay(searchDelay).ContinueWith((_) => new SearchDevicesResult(device1And2Result)));

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(device3.DevAddr))
                .Returns(Task.Delay(searchDelay).ContinueWith((_) => new SearchDevicesResult(new IoTHubDeviceInfo(device3.DevAddr, device3.DevEUI, "3").AsList())));

            this.LoRaDeviceApi.Setup(x => x.SearchByDevAddrAsync(device4.DevAddr))
                .Returns(Task.Delay(searchDelay).ContinueWith((_) => new SearchDevicesResult(new IoTHubDeviceInfo(device4.DevAddr, device4.DevEUI, "3").AsList())));

            var deviceClient1 = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var deviceClient1Telemetry = new List<LoRaDeviceTelemetry>();
            deviceClient1.Setup(x => x.GetTwinAsync())
                .Returns(Task.Delay(getTwinDelay).ContinueWith((_) => device1Twin));
            deviceClient1.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, d) => deviceClient1Telemetry.Add(t))
                .Returns(Task.Delay(sendMessageDelay).ContinueWith((_) => true));
            deviceClient1.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .Returns(Task.Delay(receiveDelay).ContinueWith((_) => (Message)null));

            var deviceClient2 = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var deviceClient2Telemetry = new List<LoRaDeviceTelemetry>();
            deviceClient2.Setup(x => x.GetTwinAsync())
                .Returns(Task.Delay(getTwinDelay).ContinueWith((_) => device2Twin));
            deviceClient2.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, d) => deviceClient2Telemetry.Add(t))
                .Returns(Task.Delay(sendMessageDelay).ContinueWith((_) => true));
            deviceClient2.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .Returns(Task.Delay(receiveDelay).ContinueWith((_) => (Message)null));

            var deviceClient3 = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var deviceClient3Telemetry = new List<LoRaDeviceTelemetry>();
            deviceClient3.Setup(x => x.GetTwinAsync())
                .Returns(Task.Delay(getTwinDelay).ContinueWith((_) => device3Twin));
            deviceClient3.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, d) => deviceClient3Telemetry.Add(t))
                .Returns(Task.Delay(sendMessageDelay).ContinueWith((_) => true));
            deviceClient3.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .Returns(Task.Delay(receiveDelay).ContinueWith((_) => (Message)null));

            var deviceClient4 = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            var deviceClient4Telemetry = new List<LoRaDeviceTelemetry>();
            deviceClient4.Setup(x => x.GetTwinAsync())
                .Returns(Task.Delay(getTwinDelay).ContinueWith((_) => device4Twin));
            deviceClient4.Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((t, d) => deviceClient4Telemetry.Add(t))
                .Returns(Task.Delay(sendMessageDelay).ContinueWith((_) => true));
            deviceClient4.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                .Returns(Task.Delay(receiveDelay).ContinueWith((_) => (Message)null));

            this.LoRaDeviceFactory.SetClient(device1.DevEUI, deviceClient1.Object);
            this.LoRaDeviceFactory.SetClient(device2.DevEUI, deviceClient2.Object);
            this.LoRaDeviceFactory.SetClient(device3.DevEUI, deviceClient3.Object);
            this.LoRaDeviceFactory.SetClient(device4.DevEUI, deviceClient4.Object);

            var deviceRegistry = new LoRaDeviceRegistry(this.ServerConfiguration, this.NewMemoryCache(), this.LoRaDeviceApi.Object, this.LoRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(
                this.ServerConfiguration,
                deviceRegistry,
                this.FrameCounterUpdateStrategyProvider);

            var device1Messages = await this.SendMessages(device1, messageDispatcher, payloadInitialFcnt, delayBetweenMessages);
            var device2Messages = await this.SendMessages(device2, messageDispatcher, payloadInitialFcnt, delayBetweenMessages);
            var device3Messages = await this.SendMessages(device3, messageDispatcher, payloadInitialFcnt, delayBetweenMessages);
            var device4Messages = await this.SendMessages(device4, messageDispatcher, payloadInitialFcnt, delayBetweenMessages);

            var allMessages = device1Messages
                .Concat(device2Messages)
                .Concat(device3Messages)
                .Concat(device4Messages)
                .ToList();

            await Task.WhenAll(allMessages.Select(x => x.WaitCompleteAsync()));

            Assert.All(allMessages, m => Assert.True(m.ProcessingSucceeded));

            var telemetries = new[]
            {
                deviceClient1Telemetry,
                deviceClient2Telemetry,
                deviceClient3Telemetry,
                deviceClient4Telemetry
            };

            foreach (var telemetry in telemetries)
            {
                Assert.Equal(5, telemetry.Count);
                Assert.Equal(payloadInitialFcnt, telemetry[0].Fcnt);
                Assert.Equal(payloadInitialFcnt + 1, telemetry[1].Fcnt);
                Assert.Equal(payloadInitialFcnt + 2, telemetry[2].Fcnt);
                Assert.Equal(payloadInitialFcnt + 3, telemetry[3].Fcnt);
                Assert.Equal(payloadInitialFcnt + 4, telemetry[4].Fcnt);
            }

            deviceClient1.VerifyAll();
            deviceClient2.VerifyAll();
            deviceClient3.VerifyAll();
            deviceClient4.VerifyAll();
            this.LoRaDeviceApi.VerifyAll();
        }
    }
}