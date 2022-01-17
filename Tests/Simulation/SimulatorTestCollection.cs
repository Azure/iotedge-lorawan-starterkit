// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaWan.Tests.Common;
    using Xunit;

    // Tests ABP requests
    [Trait("Category", "SkipWhenLiveUnitTesting")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    // False positive, the name is accurate in the context of xUnit collections.
    public sealed class SimulatorTestCollection : IntegrationTestBaseSim
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        private static readonly TimeSpan IntervalBetweenMessages = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan IntervalAfterJoin = TimeSpan.FromSeconds(10);

        public TestConfiguration Configuration { get; } = TestConfiguration.GetConfiguration();

        public SimulatorTestCollection(IntegrationTestFixtureSim testFixture)
            : base(testFixture)
        { }

        /// <summary>
        /// This test needs to be reworked. It was commented out in the previous code, I guess this was supposed to be a mini load test.
        /// However all the method calls where non existing
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Ten_Devices_Sending_Messages_At_Same_Time()
        {
            var deviceTasks = new List<Task>();
            var simulatedDeviceList = new List<SimulatedDevice>();
            foreach (var device in TestFixtureSim.DeviceRange1000_ABP)
            {
                var simulatedDevice = new SimulatedDevice(device, simulatedBasicsStation: TestFixtureSim.SimulatedBasicsStations);
                simulatedDeviceList.Add(simulatedDevice);
                using var request = WaitableLoRaRequest.CreateWaitableRequest(simulatedDevice.CreateConfirmedDataUpMessage(device.DeviceID));

                deviceTasks.Add(simulatedDevice.SendDataMessageAsync(request));
            }

            await Task.WhenAll(deviceTasks);
            await Task.Delay(TimeSpan.FromSeconds(5));

            var eventsByDevices = TestFixture.IoTHubMessages.Events.GroupBy(x => x.SystemProperties["iothub-connection-device-id"]);
            // There are 11 devices in that group 0-10
            Assert.Equal(11, eventsByDevices.Count());

            foreach (var device in simulatedDeviceList)
            {
                device.EnsureMessageResponsesAreReceived(1);
            }
        }

        [Fact]
        public async Task Single_ABP_Simulated_Device()
        {
            const int MessageCount = 5;

            var device = TestFixtureSim.Device1001_Simulated_ABP;
            var simulatedDevice = new SimulatedDevice(device, simulatedBasicsStation: TestFixtureSim.SimulatedBasicsStations);

            for (var i = 1; i <= MessageCount; i++)
            {
                using var request = WaitableLoRaRequest.CreateWaitableRequest(simulatedDevice.CreateConfirmedDataUpMessage(i.ToString(CultureInfo.InvariantCulture)));
                await simulatedDevice.SendDataMessageAsync(request);
                await Task.Delay(IntervalBetweenMessages);
            }

            var actualAmountOfMsgs = TestFixture.IoTHubMessages.Events.Count(x => x.GetDeviceId() == simulatedDevice.LoRaDevice.DeviceID && !x.Properties.ContainsKey("iothub-message-schema"));
            Assert.Equal(MessageCount * TestFixtureSim.SimulatedBasicsStations.Count, actualAmountOfMsgs);
            Assert.True(simulatedDevice.EnsureMessageResponsesAreReceived(MessageCount));
        }

        [Fact]
        public async Task Single_OTAA_Simulated_Device()
        {
            const int MessageCount = 5;

            var device = TestFixtureSim.Device1002_Simulated_OTAA;
            var simulatedDevice = new SimulatedDevice(device, simulatedBasicsStation: TestFixtureSim.SimulatedBasicsStations);

            using var request = WaitableLoRaRequest.CreateWaitableRequest(simulatedDevice.CreateJoinRequest());

            var joined = await simulatedDevice.JoinAsync(request);
            Assert.True(joined, "OTAA join failed");

            await Task.Delay(IntervalAfterJoin);

            for (var i = 1; i <= MessageCount; i++)
            {
                using var request2 = WaitableLoRaRequest.CreateWaitableRequest(simulatedDevice.CreateConfirmedDataUpMessage(i.ToString(CultureInfo.InvariantCulture)));
                await simulatedDevice.SendDataMessageAsync(request2);
                await Task.Delay(IntervalBetweenMessages);
            }

            // wait 10 seconds before checking if iot hub content is available
            await Task.Delay(TimeSpan.FromSeconds(10));

            var actualAmountOfMsgs = TestFixture.IoTHubMessages.Events.Count(x => x.GetDeviceId() == simulatedDevice.LoRaDevice.DeviceID && !x.Properties.ContainsKey("iothub-message-schema"));
            Assert.Equal(MessageCount * TestFixtureSim.DeviceRange5000_BasicsStationSimulators.Count, actualAmountOfMsgs);
            Assert.True(simulatedDevice.EnsureMessageResponsesAreReceived(MessageCount + 1));
        }

        [Fact]
        public async Task Lots_Of_Devices_OTAA_Simulated_Load_Test()
        {
            var messageCounts = 50;
            var deviceTasks = new List<Task>();
            var simulatedDeviceList = new List<SimulatedDevice>();
            foreach (var device in TestFixtureSim.DeviceRange4000_500_OTAA)
            {
                var simulatedDevice = new SimulatedDevice(device, simulatedBasicsStation: TestFixtureSim.SimulatedBasicsStations);
                simulatedDeviceList.Add(simulatedDevice);

                deviceTasks.Add(Task.Run(async () =>
                {
                    using var requestForJoin = WaitableLoRaRequest.CreateWaitableRequest(simulatedDevice.CreateJoinRequest());
                    var joined = await simulatedDevice.JoinAsync(requestForJoin);
                    Assert.True(joined, "OTAA join failed");

                    for (var i = 0; i < messageCounts; i++)
                    {
                        using var request = WaitableLoRaRequest.CreateWaitableRequest(simulatedDevice.CreateConfirmedDataUpMessage(device.DeviceID));
                        await simulatedDevice.SendDataMessageAsync(request);
                        await Task.Delay(IntervalBetweenMessages);
                    }
                }));
            }

            await Task.WhenAll(deviceTasks);
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Ensuring IoT Hub received everything it needed
            var eventsByDevices = TestFixture.IoTHubMessages.Events.GroupBy(x => x.SystemProperties["iothub-connection-device-id"]).ToList();
            Assert.Equal(simulatedDeviceList.Count, eventsByDevices.Count);
            foreach(var g in eventsByDevices)
            {
                Assert.Equal(messageCounts * TestFixtureSim.DeviceRange5000_BasicsStationSimulators.Count, g.Count() );
            }

            // Ensured BasicsStation received everything needed
            foreach (var device in simulatedDeviceList)
            {
                device.EnsureMessageResponsesAreReceived(messageCounts + 1);
            }
        }

        //[Fact(Skip = "simulated")]
        //public async Task Simulated_Http_Based_Decoder_Scenario()
        //{
        //    var device = TestFixtureSim.Device1003_Simulated_HttpBasedDecoder;
        //    var simulatedDevice = new Common.SimulatedDevice(device);
        //    var networkServerIPEndpoint = CreateNetworkServerEndpoint();

        //    using (var simulatedPacketForwarder = new SimulatedBasicsStation(device.DeviceID, networkServerIPEndpoint))
        //    {
        //        await simulatedPacketForwarder.StartAsync();

        //        var joined = await simulatedDevice.JoinAsync(simulatedPacketForwarder);
        //        Assert.True(joined, "OTAA join failed");

        //        await Task.Delay(this.intervalAfterJoin);

        //        for (var i = 1; i <= 3; i++)
        //        {
        //            using var request = WaitableLoRaRequest.CreateWaitableRequest(simulatedDevice.CreateConfirmedDataUpMessage(i.ToString(CultureInfo.InvariantCulture)));
        //            await simulatedPacketForwarder.SendDataMessageAsync(request);
        //            await Task.Delay(this.intervalBetweenMessages);
        //        }

        //        await simulatedPacketForwarder.StopAsync();
        //    }

        //    // wait 10 seconds before checking if iot hub content is available
        //    await Task.Delay(TimeSpan.FromSeconds(10));
        //}

        //// Scenario:
        //// - 100x ABP devices
        //// - 10x OTAA devices
        //// - Sending unconfirmed messages
        //// - Goal: 20 devices in parallel
        //[Fact]
        //public async Task Multiple_ABP_and_OTAA_Simulated_Devices_Unconfirmed()
        //{
        //    // amount of devices to test with. Maximum is 89
        //    // Do go beyond 100 deviceClients in IoT Edge, use edgeHub env var 'MaxConnectedClients'
        //    var scenarioDeviceNumber = 250; // Default: 20

        //    // amount of messages to send per device (without warm-up phase)
        //    var scenarioMessagesPerDevice = 10; // Default: 10

        //    // amount of devices to send data in parallel
        //    var scenarioDeviceStepSize = 20; // Default: 20

        //    // amount of devices to send data in parallel for the warm-up phase
        //    var warmUpDeviceStepSize = 2; // Default: 10

        //    // amount of messages to send before device join is to occur
        //    var messagesBeforeJoin = 10; // Default: 10

        //    // amount of Unconfirmed messges to send before Confirmed message is to occur
        //    var messagesBeforeConfirmed = 5; // Default: 5

        //    // amount of seconds to wait between sends in warmup phase
        //    var delayWarmup = 5 * 1000; // Default 5 * 1000

        //    // amount of seconds to wait between sends in main phase
        //    var delayMessageSending = 5 * 1000; // Default 5 * 1000

        //    // amount of miliseconds to wait before checking LoRaWanNetworkSrvModule
        //    // for successful sending of messages to IoT Hub.
        //    // delay for 100 devices: 2 * 60 * 1000
        //    // delay for 20 devices: 15 * 1000
        //    var delayNetworkServerCheck = 15 * 60 * 1000;

        //    // amount of miliseconds to wait before checking of messages in IoT Hub
        //    // delay for 100 devices: 1 * 60 * 1000
        //    // delay for 20 devices: 15 * 1000
        //    var delayIoTHubCheck = 5 * 60 * 1000;

        //    // Get random number seed
        //    var rnd = new Random();
        //    var seed = rnd.Next(100, 999);

        //    var count = 0;
        //    var listSimulatedDevices = new List<Common.SimulatedDevice>();
        //    foreach (var device in TestFixtureSim.DeviceRange2000_1000_ABP)
        //    {
        //        if (count < scenarioDeviceNumber)
        //        {
        //            var simulatedDevice = new Common.SimulatedDevice(device);
        //            listSimulatedDevices.Add(simulatedDevice);
        //            count++;
        //        }
        //    }

        //    var totalDevices = listSimulatedDevices.Count;
        //    var totalJoinDevices = 0;
        //    var totalJoins = 0;

        //    var listSimulatedJoinDevices = new List<Common.SimulatedDevice>();
        //    foreach (var joinDevice in TestFixtureSim.DeviceRange3000_10_OTAA)
        //    {
        //        var simulatedJoinDevice = new Common.SimulatedDevice(joinDevice);
        //        listSimulatedJoinDevices.Add(simulatedJoinDevice);
        //    }

        //    var networkServerIPEndpoint = CreateNetworkServerEndpoint();

        //    using (var simulatedBasicsStation = new SimulatedBasicsStation("b827:ebff:fea3:be42", networkServerIPEndpoint))
        //    {
        //        await simulatedBasicsStation.StartAsync();

        //        // 1. picking 2x devices send an initial message (warm device cache in NtwSrv module)
        //        //    timeout of 2 seconds between each loop
        //        var tasks = new List<Task>();
        //        for (var i = 0; i < totalDevices;)
        //        {
        //            tasks.Clear();
        //            foreach (var device in listSimulatedDevices.Skip(i).Take(warmUpDeviceStepSize))
        //            {
        //                await Task.Delay(rnd.Next(10, 250)); // Sleep between 10 and 250ms

        //                TestLogger.Log($"[WARM-UP] {device.LoRaDevice.DeviceID}");
        //                using var request = WaitableLoRaRequest.CreateWaitableRequest(device.CreateConfirmedDataUpMessage(seed + "000"));

        //                tasks.Add(simulatedBasicsStation.SendDataMessageAsync(request));
        //            }

        //            await Task.WhenAll(tasks);
        //            await Task.Delay(delayWarmup);

        //            i += warmUpDeviceStepSize;
        //        }

        //        // 2. picking 20x devices sends messages (send 10 messages for each device)
        //        //    timeout of 5 seconds between each
        //        var messageCounter = 1;
        //        var joinDevice = 0;

        //        for (var messageId = 1; messageId <= scenarioMessagesPerDevice; ++messageId)
        //        {
        //            for (var i = 0; i < totalDevices;)
        //            {
        //                tasks.Clear();
        //                var payload = seed + messageId.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0');

        //                foreach (var device in listSimulatedDevices.Skip(i).Take(scenarioDeviceStepSize))
        //                {
        //                    await Task.Delay(rnd.Next(10, 250)); // Sleep between 10 and 250ms

        //                    if (messageCounter % messagesBeforeConfirmed != 0)
        //                    {
        //                        // send Unconfirmed message
        //                        using var request = WaitableLoRaRequest.CreateWaitableRequest(device.CreateUnconfirmedDataUpMessage(payload));

        //                        tasks.Add(simulatedBasicsStation.SendDataMessageAsync(request));
        //                    }
        //                    else
        //                    {
        //                        using var request = WaitableLoRaRequest.CreateWaitableRequest(device.CreateConfirmedDataUpMessage(payload));

        //                        // send Confirmed message, not waiting for confirmation
        //                        tasks.Add(simulatedBasicsStation.SendDataMessageAsync(request));
        //                    }

        //                    if (messageCounter % messagesBeforeJoin == 0)
        //                    {
        //                        await Task.Delay(rnd.Next(10, 250)); // Sleep between 10 and 250ms

        //                        // Send Join Request with waiting
        //                        // tasks.Add(listSimulatedJoinDevices[joinDevice].JoinAsync(simulatedPacketForwarder));

        //                        // Send Join Request without waiting
        //                        _ = listSimulatedJoinDevices[joinDevice].JoinAsync(simulatedBasicsStation);
        //                        totalJoins++;

        //                        TestLogger.Log($"[INFO] Join request sent for {listSimulatedJoinDevices[joinDevice].LoRaDevice.DeviceID}");

        //                        joinDevice = (joinDevice == 9) ? 0 : joinDevice + 1;
        //                        totalJoinDevices++; // Number corrected below.
        //                    }

        //                    messageCounter++;
        //                }

        //                await Task.WhenAll(tasks);
        //                await Task.Delay(delayMessageSending);

        //                i += scenarioDeviceStepSize;
        //            }
        //        }

        //        await simulatedBasicsStation.StopAsync();
        //    }

        //    // Correct total number of JoinDevices back to a max. of 10
        //    totalJoinDevices = (totalJoinDevices > 10) ? 10 : totalJoinDevices;

        //    // Wait before executing to allow for all messages to be sent
        //    TestLogger.Log($"[INFO] Waiting for {delayNetworkServerCheck / 1000} sec. before the test continues...");
        //    await Task.Delay(delayNetworkServerCheck);

        //    // 3. test Network Server logs if messages have been sent successfully
        //    string expectedPayload;
        //    foreach (var device in listSimulatedDevices)
        //    {
        //        TestLogger.Log($"[INFO] Looking for upstream messages for {device.LoRaDevice.DeviceID}");
        //        for (var messageId = 0; messageId <= scenarioMessagesPerDevice; ++messageId)
        //        {
        //            // Find "<all Device ID>: message '{"value":<seed+0 to number of msg/device>}' sent to hub" in network server logs
        //            expectedPayload = $"{device.LoRaDevice.DeviceID}: message '{{\"value\":{seed + messageId.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0')}}}' sent to hub";
        //            _ = await TestFixture.AssertNetworkServerModuleLogStartsWithAsync(expectedPayload);
        //        }
        //    }

        //    TestLogger.Log($"[INFO] Waiting for {delayIoTHubCheck / 1000} sec. before the test continues...");
        //    await Task.Delay(delayIoTHubCheck);

        //    // IoT Hub test for arrival of messages.
        //    var eventsByDevices = TestFixture.IoTHubMessages.Events
        //                              .GroupBy(x => x.SystemProperties["iothub-connection-device-id"])
        //                              .ToDictionary(x => x.Key, x => x.ToList());

        //    // 4. Check that we have the right amount of devices receiving messages in IoT Hub
        //    // Assert.Equal(totalDevices, eventsByDevices.Count());
        //    if (totalDevices == eventsByDevices.Count)
        //    {
        //        TestLogger.Log($"[INFO] Devices sending messages: {totalDevices + totalJoinDevices}, == Devices receiving messages in IoT Hub: {eventsByDevices.Count}");
        //    }
        //    else
        //    {
        //        TestLogger.Log($"[WARN] Devices sending messages: {totalDevices + totalJoinDevices}, != Devices receiving messages in IoT Hub: {eventsByDevices.Count}");
        //    }

        //    // 5. Check that the correct number of messages have arrived in IoT Hub per device
        //    //    Warn only.
        //    foreach (var device in listSimulatedDevices)
        //    {
        //        // Assert.True(
        //        //    eventsByDevices.TryGetValue(device.LoRaDevice.DeviceID, out var events),
        //        //    $"No messages were found for device {device.LoRaDevice.DeviceID}");
        //        // if (events.Count > 0)
        //        if (eventsByDevices.TryGetValue(device.LoRaDevice.DeviceID, out var events))
        //        {
        //            var actualAmountOfMsgs = events.Where(x => !x.Properties.ContainsKey("iothub-message-schema")).Count();
        //            // Assert.Equal((1 + scenarioMessagesPerDevice), actualAmountOfMsgs);
        //            if ((1 + scenarioMessagesPerDevice) != actualAmountOfMsgs)
        //            {
        //                TestLogger.Log($"[WARN] Wrong events for device {device.LoRaDevice.DeviceID}. Actual: {actualAmountOfMsgs}. Expected {1 + scenarioMessagesPerDevice}");
        //            }
        //            else
        //            {
        //                TestLogger.Log($"[INFO] Correct events for device {device.LoRaDevice.DeviceID}. Actual: {actualAmountOfMsgs}. Expected {1 + scenarioMessagesPerDevice}");
        //            }
        //        }
        //        else
        //        {
        //            TestLogger.Log($"[WARN] No messages were found for device {device.LoRaDevice.DeviceID}");
        //        }
        //    }

        //    // 6. Check if all expected messages have arrived in IoT Hub
        //    //    Warn only.
        //    foreach (var device in listSimulatedDevices)
        //    {
        //        TestLogger.Log($"[INFO] Looking for IoT Hub messages for {device.LoRaDevice.DeviceID}");
        //        for (var messageId = 0; messageId <= scenarioMessagesPerDevice; ++messageId)
        //        {
        //            // Find message containing '{"value":<seed>.<0 to number of msg/device>}' for all leaf devices in IoT Hub
        //            expectedPayload = $"{{\"value\":{seed + messageId.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0')}}}";
        //            await TestFixture.AssertIoTHubDeviceMessageExistsAsync(device.LoRaDevice.DeviceID, expectedPayload);
        //        }
        //    }
        //}
    }
}
