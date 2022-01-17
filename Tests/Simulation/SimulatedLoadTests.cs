// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.EventHubs;
    using Xunit;

    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class SimulatedLoadTests : IntegrationTestBaseSim, IAsyncLifetime
    {
        private static readonly TimeSpan IntervalBetweenMessages = TimeSpan.FromSeconds(5);
        private readonly List<SimulatedBasicsStation> simulatedBasicsStations;
        /// <summary>
        /// A unique upstream message fragment is used for each uplink message to ensure
        /// that there is no interference between test runs.
        /// </summary>
        private readonly string uniqueMessageFragment;

        public TestConfiguration Configuration { get; } = TestConfiguration.GetConfiguration();

        public SimulatedLoadTests(IntegrationTestFixtureSim testFixture)
            : base(testFixture)
        {
            this.uniqueMessageFragment = Guid.NewGuid().ToString();
            this.simulatedBasicsStations =
                testFixture.DeviceRange5000_BasicsStationSimulators
                           .Select((basicsStation, i) => (BasicsStation: basicsStation, Index: i))
                           .Select(b => new SimulatedBasicsStation(StationEui.Parse(b.BasicsStation.DeviceID), Configuration.LnsEndpointsForSimulator[b.Index % Configuration.LnsEndpointsForSimulator.Count]))
                           .ToList();
        }

        /// <summary>
        /// This test needs to be reworked. It was commented out in the previous code, I guess this was supposed to be a mini load test.
        /// However all the method calls where non existing
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Ten_Devices_Sending_Messages_At_Same_Time()
        {
            // arrange
            const int messageCount = 1;
            var simulatedDevices = InitializeSimulatedDevices(TestFixtureSim.DeviceRange1000_ABP);
            Assert.NotEmpty(simulatedDevices);

            // act
            await Task.WhenAll(from device in simulatedDevices
                               select SendConfirmedUpstreamMessages(device, messageCount));
            await WaitForResultsInIotHubAsync();

            // assert
            foreach (var device in simulatedDevices)
            {
                Assert.Equal(GetExpectedMessageCount(device.LoRaDevice.Deduplication, messageCount), TestFixture.IoTHubMessages.Events.Count(eventData => ContainsMessageFromDevice(eventData, device)));
                device.EnsureMessageResponsesAreReceived(1);
            }
        }

        [Fact]
        public async Task Single_ABP_Simulated_Device()
        {
            const int messageCount = 5;
            var device = new SimulatedDevice(TestFixtureSim.Device1001_Simulated_ABP, simulatedBasicsStation: this.simulatedBasicsStations);

            await SendConfirmedUpstreamMessages(device, messageCount);
            await WaitForResultsInIotHubAsync();

            Assert.Equal(GetExpectedMessageCount(device.LoRaDevice.Deduplication, messageCount), TestFixture.IoTHubMessages.Events.Count(eventData => ContainsMessageFromDevice(eventData, device)));
            device.EnsureMessageResponsesAreReceived(messageCount);
        }

        [Fact]
        public async Task Single_OTAA_Simulated_Device()
        {
            const int messageCount = 5;
            var device = new SimulatedDevice(TestFixtureSim.Device1002_Simulated_OTAA, simulatedBasicsStation: this.simulatedBasicsStations);

            Assert.True(await device.JoinAsync(), "OTAA join failed");
            await SendConfirmedUpstreamMessages(device, messageCount);
            await WaitForResultsInIotHubAsync();

            Assert.Equal(GetExpectedMessageCount(device.LoRaDevice.Deduplication, messageCount), TestFixture.IoTHubMessages.Events.Count(eventData => ContainsMessageFromDevice(eventData, device)));
            device.EnsureMessageResponsesAreReceived(messageCount + 1);
        }

        [Fact]
        public async Task Lots_Of_Devices_OTAA_Simulated_Load_Test()
        {
            // arrange
            const int messageCounts = 50;
            var simulatedDevices = InitializeSimulatedDevices(TestFixtureSim.DeviceRange4000_OTAA_FullLoad);
            Assert.NotEmpty(simulatedDevices);

            // act
            var offsetInterval = IntervalBetweenMessages / simulatedDevices.Count;
            await Task.WhenAll(from deviceWithOffset in simulatedDevices.Select((device, i) => (Device: device, Offset: i * offsetInterval))
                               select ActAsync(deviceWithOffset.Device, deviceWithOffset.Offset));

            async Task ActAsync(SimulatedDevice device, TimeSpan startOffset)
            {
                await Task.Delay(startOffset);
                Assert.True(await device.JoinAsync(), "OTAA join failed");
                await SendConfirmedUpstreamMessages(device, messageCounts);
            }

            await WaitForResultsInIotHubAsync();

            // assert
            foreach (var device in simulatedDevices)
            {
                Assert.Equal(GetExpectedMessageCount(device.LoRaDevice.Deduplication, messageCounts), TestFixture.IoTHubMessages.Events.Count(e => ContainsMessageFromDevice(e, device)));
                device.EnsureMessageResponsesAreReceived(messageCounts + 1);
            }
        }

        private async Task SendConfirmedUpstreamMessages(SimulatedDevice device, int count)
        {
            for (var i = 0; i < count; ++i)
            {
                using var request = CreateConfirmedUpstreamMessage(device);
                await device.SendDataMessageAsync(request);
                await Task.Delay(IntervalBetweenMessages);
            }
        }

        private int GetExpectedMessageCount(string deduplicationMode, int numberOfMessagesPerDevice) =>
            deduplicationMode?.ToUpperInvariant() switch
            {
                null or "" or "NONE" => numberOfMessagesPerDevice * this.simulatedBasicsStations.Count,
                "MARK" => numberOfMessagesPerDevice * this.simulatedBasicsStations.Count,
                "DROP" => numberOfMessagesPerDevice,
                _ => throw new NotImplementedException()
            };

        private WaitableLoRaRequest CreateConfirmedUpstreamMessage(SimulatedDevice simulatedDevice) =>
            WaitableLoRaRequest.CreateWaitableRequest(simulatedDevice.CreateConfirmedDataUpMessage(this.uniqueMessageFragment));

        private static Task WaitForResultsInIotHubAsync() => Task.Delay(TimeSpan.FromSeconds(10));

        private bool ContainsMessageFromDevice(EventData eventData, SimulatedDevice simulatedDevice)
        {
            if (eventData.Properties.ContainsKey("iothub-message-schema")) return false;
            if (eventData.GetDeviceId() != simulatedDevice.LoRaDevice.DeviceID) return false;
            return Encoding.UTF8.GetString(eventData.Body).Contains(this.uniqueMessageFragment, StringComparison.Ordinal);
        }

        private List<SimulatedDevice> InitializeSimulatedDevices(IReadOnlyCollection<TestDeviceInfo> testDeviceInfos) =>
            testDeviceInfos.Select(d => new SimulatedDevice(d, simulatedBasicsStation: this.simulatedBasicsStations)).ToList();

        public async Task InitializeAsync()
        {
            await Task.WhenAll(from basicsStation in this.simulatedBasicsStations
                               select basicsStation.StartAsync());
        }

        public async Task DisposeAsync()
        {
            foreach (var basicsStation in this.simulatedBasicsStations)
            {
                try
                {
                    await basicsStation.StopAsync();
                    basicsStation.Dispose();
                }
                catch (Exception)
                {
                    // Dispose all basics stations
                }
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
