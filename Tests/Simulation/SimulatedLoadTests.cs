﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Extensions.Logging;
    using Xunit;
    using Xunit.Abstractions;
    using static MoreLinq.Extensions.BatchExtension;
    using static MoreLinq.Extensions.IndexExtension;
    using static MoreLinq.Extensions.TransposeExtension;

    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class SimulatedLoadTests : IntegrationTestBaseSim, IAsyncLifetime
    {
        private const double DownstreamDroppedMessagesTolerance = 0.02;
        private static readonly TimeSpan IntervalBetweenMessages = TimeSpan.FromSeconds(5);
        private readonly List<SimulatedBasicsStation> simulatedBasicsStations;
        /// <summary>
        /// A unique upstream message fragment is used for each uplink message to ensure
        /// that there is no interference between test runs.
        /// </summary>
        private readonly string uniqueMessageFragment;
        private readonly TestOutputLogger logger;

        public TestConfiguration Configuration { get; } = TestConfiguration.GetConfiguration();

        public SimulatedLoadTests(IntegrationTestFixtureSim testFixture, ITestOutputHelper testOutputHelper)
            : base(testFixture)
        {
            this.uniqueMessageFragment = Guid.NewGuid().ToString();
            this.logger = new TestOutputLogger(testOutputHelper);
            this.simulatedBasicsStations =
                testFixture.DeviceRange5000_BasicsStationSimulators.Index()
                           .Select(b => new SimulatedBasicsStation(StationEui.Parse(b.Value.DeviceID), Configuration.LnsEndpointsForSimulator[b.Key % Configuration.LnsEndpointsForSimulator.Count]))
                           .ToList();

            Assert.True(this.simulatedBasicsStations.Count % Configuration.LnsEndpointsForSimulator.Count == 0, "Since Basics Stations are round-robin distributed to LNS, we must have the same number of stations per LNS for well-defined test assertions.");
        }

        [Fact]
        public async Task Ten_Devices_Sending_Messages_At_Same_Time()
        {
            // arrange
            const int messageCount = 2;
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
                EnsureMessageResponsesAreReceived(device, messageCount);
            }
        }

        [Fact]
        public async Task Single_ABP_Simulated_Device()
        {
            const int messageCount = 5;
            var device = new SimulatedDevice(TestFixtureSim.Device1001_Simulated_ABP, simulatedBasicsStation: this.simulatedBasicsStations, logger: this.logger);

            await SendConfirmedUpstreamMessages(device, messageCount);
            await WaitForResultsInIotHubAsync();

            Assert.Equal(GetExpectedMessageCount(device.LoRaDevice.Deduplication, messageCount), TestFixture.IoTHubMessages.Events.Count(eventData => ContainsMessageFromDevice(eventData, device)));
            EnsureMessageResponsesAreReceived(device, messageCount);
        }

        [Fact]
        public async Task Single_OTAA_Simulated_Device()
        {
            const int messageCount = 5;
            var device = new SimulatedDevice(TestFixtureSim.Device1002_Simulated_OTAA, simulatedBasicsStation: this.simulatedBasicsStations, logger: this.logger);

            Assert.True(await device.JoinAsync(), "OTAA join failed");
            await SendConfirmedUpstreamMessages(device, messageCount);
            await WaitForResultsInIotHubAsync();

            Assert.Equal(GetExpectedMessageCount(device.LoRaDevice.Deduplication, messageCount), TestFixture.IoTHubMessages.Events.Count(eventData => ContainsMessageFromDevice(eventData, device)));
            EnsureMessageResponsesAreReceived(device, messageCount + 1);
        }

        [Fact]
        public async Task Lots_Of_Devices_OTAA_Simulated_Load_Test()
        {
            // arrange
            const int messageCounts = 20;
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
                EnsureMessageResponsesAreReceived(device, messageCounts + 1);
            }
        }

        /// <summary>
        /// This test emulates a scenario where we have several plants with multiple concentrators and devices,
        /// connected to several gateways (either on-premises or in the cloud).
        /// </summary>
        [Fact]
        public async Task Connected_Factory_Load_Test_Scenario()
        {
            // 100 devices, 4 concentrators, 1 gateway, NO CONNECTION TIMEOUT.
            // No Pool size and no maxconnected clients set on edge hub.
            /*
             *  Standard Output: 
Starting join phase at 01/27/2022 12:39:44.
Running cycle 1 of 7 at 01/27/2022 12:40:49.
Running cycle 2 of 7 at 01/27/2022 12:41:22.
Running cycle 3 of 7 at 01/27/2022 12:41:42.
Running cycle 4 of 7 at 01/27/2022 12:42:02.
Running cycle 5 of 7 at 01/27/2022 12:42:22.
Running cycle 6 of 7 at 01/27/2022 12:42:42.
Running cycle 7 of 7 at 01/27/2022 12:43:01.
Sent 800 messages in 217.1627796 seconds.
Message counts by device ID:
{"0300000000009000":7,"0300000000009001":7,"0300000000009002":7,"0300000000009003":7,"0300000000009004":7,"0300000000009005":7,"0300000000009006":7,"0300000000009007":7,"0300000000009008":7,"0300000000009009":7,"0300000000009010":7,"0300000000009011":7,"0300000000009012":7,"0300000000009013":7,"0300000000009014":7,"0300000000009015":7,"0300000000009016":7,"0300000000009017":7,"0300000000009018":7,"0300000000009019":7,"0300000000009020":7,"0300000000009021":7,"0300000000009022":7,"0300000000009023":7,"0300000000009024":7,"0300000000009025":7,"0300000000009026":7,"0300000000009027":7,"0300000000009028":7,"0300000000009029":7,"0300000000009030":7,"0300000000009031":7,"0300000000009032":7,"0300000000009033":7,"0300000000009034":7,"0300000000009035":7,"0300000000009036":7,"0300000000009037":7,"0300000000009038":7,"0300000000009039":7,"0300000000009040":7,"0300000000009041":7,"0300000000009042":7,"0300000000009043":7,"0300000000009044":7,"0300000000009045":7,"0300000000009046":7,"0300000000009047":7,"0300000000009048":7,"0300000000009049":7,"0300000000009050":7,"0300000000009051":7,"0300000000009052":7,"0300000000009053":7,"0300000000009054":7,"0300000000009055":7,"0300000000009056":7,"0300000000009057":7,"0300000000009058":7,"0300000000009059":7,"0300000000009060":7,"0300000000009061":7,"0300000000009062":7,"0300000000009063":7,"0300000000009064":7,"0300000000009065":7,"0300000000009066":7,"0300000000009067":7,"0300000000009068":7,"0300000000009069":7,"0300000000009070":7,"0300000000009071":7,"0300000000009072":7,"0300000000009073":7,"0300000000009074":7,"0300000000009075":7,"0300000000009076":7,"0300000000009077":7,"0300000000009078":7,"0300000000009079":7,"0300000000009080":7,"0300000000009081":7,"0300000000009082":7,"0300000000009083":7,"0300000000009084":7,"0300000000009085":7,"0300000000009086":7,"0300000000009087":7,"0300000000009088":7,"0300000000009089":7,"0300000000009090":7,"0300000000009091":7,"0300000000009092":7,"0300000000009093":7,"0300000000009094":7,"0300000000009095":7,"0300000000009096":7,"0300000000009097":7,"0300000000009098":7,"0300000000009099":7}
Asserting device 0300000000009000 (1/100)
Asserting device 0300000000009001 (2/100)
Asserting device 0300000000009002 (3/100)
Asserting device 0300000000009003 (4/100)
Asserting device 0300000000009004 (5/100)
Asserting device 0300000000009005 (6/100)
Asserting device 0300000000009006 (7/100)
Asserting device 0300000000009007 (8/100)
Asserting device 0300000000009008 (9/100)
Asserting device 0300000000009009 (10/100)
Asserting device 0300000000009010 (11/100)
Asserting device 0300000000009011 (12/100)
Asserting device 0300000000009012 (13/100)
Asserting device 0300000000009013 (14/100)
Asserting device 0300000000009014 (15/100)
Asserting device 0300000000009015 (16/100)
Asserting device 0300000000009016 (17/100)
Asserting device 0300000000009017 (18/100)
Asserting device 0300000000009018 (19/100)
Asserting device 0300000000009019 (20/100)
Asserting device 0300000000009020 (21/100)
Asserting device 0300000000009021 (22/100)
Asserting device 0300000000009022 (23/100)
Asserting device 0300000000009023 (24/100)
Asserting device 0300000000009024 (25/100)
Asserting device 0300000000009025 (26/100)
Asserting device 0300000000009026 (27/100)
Asserting device 0300000000009027 (28/100)
Asserting device 0300000000009028 (29/100)
Asserting device 0300000000009029 (30/100)
Asserting device 0300000000009030 (31/100)
Asserting device 0300000000009031 (32/100)
Asserting device 0300000000009032 (33/100)
Asserting device 0300000000009033 (34/100)
Asserting device 0300000000009034 (35/100)
Asserting device 0300000000009035 (36/100)
Asserting device 0300000000009036 (37/100)
Asserting device 0300000000009037 (38/100)
Asserting device 0300000000009038 (39/100)
Asserting device 0300000000009039 (40/100)
Asserting device 0300000000009040 (41/100)
Asserting device 0300000000009041 (42/100)
Asserting device 0300000000009042 (43/100)
Asserting device 0300000000009043 (44/100)
Asserting device 0300000000009044 (45/100)
Asserting device 0300000000009045 (46/100)
Asserting device 0300000000009046 (47/100)
Asserting device 0300000000009047 (48/100)
Asserting device 0300000000009048 (49/100)
Asserting device 0300000000009049 (50/100)
Asserting device 0300000000009050 (51/100)
Asserting device 0300000000009051 (52/100)
Asserting device 0300000000009052 (53/100)
Asserting device 0300000000009053 (54/100)
Asserting device 0300000000009054 (55/100)
Asserting device 0300000000009055 (56/100)
Asserting device 0300000000009056 (57/100)
Asserting device 0300000000009057 (58/100)
Asserting device 0300000000009058 (59/100)
Asserting device 0300000000009059 (60/100)
Asserting device 0300000000009060 (61/100)
Asserting device 0300000000009061 (62/100)
Asserting device 0300000000009062 (63/100)
Asserting device 0300000000009063 (64/100)
Asserting device 0300000000009064 (65/100)
Asserting device 0300000000009065 (66/100)
Asserting device 0300000000009066 (67/100)
Asserting device 0300000000009067 (68/100)
Asserting device 0300000000009068 (69/100)
Asserting device 0300000000009069 (70/100)
Asserting device 0300000000009070 (71/100)
Asserting device 0300000000009071 (72/100)
Asserting device 0300000000009072 (73/100)
Asserting device 0300000000009073 (74/100)
Asserting device 0300000000009074 (75/100)
Asserting device 0300000000009075 (76/100)
Asserting device 0300000000009076 (77/100)
Asserting device 0300000000009077 (78/100)
Asserting device 0300000000009078 (79/100)
Asserting device 0300000000009079 (80/100)
Asserting device 0300000000009080 (81/100)
Asserting device 0300000000009081 (82/100)
Asserting device 0300000000009082 (83/100)
Asserting device 0300000000009083 (84/100)
Asserting device 0300000000009084 (85/100)
Asserting device 0300000000009085 (86/100)
Asserting device 0300000000009086 (87/100)
Asserting device 0300000000009087 (88/100)
Asserting device 0300000000009088 (89/100)
Asserting device 0300000000009089 (90/100)
Asserting device 0300000000009090 (91/100)
Asserting device 0300000000009091 (92/100)
Asserting device 0300000000009092 (93/100)
Asserting device 0300000000009093 (94/100)
Asserting device 0300000000009094 (95/100)
Asserting device 0300000000009095 (96/100)
Asserting device 0300000000009096 (97/100)
Asserting device 0300000000009097 (98/100)
Asserting device 0300000000009098 (99/100)
Asserting device 0300000000009099 (100/100)
             */
            const int numberOfFactories = 1;
            const double joinsPerSecond = 3;
            var messagesPerSecond = MoreLinq.MoreEnumerable.Generate(3, old => old > 3 ? old : old + 2);
            const int numberOfLoops = 7;
            var stationsPerFactory = this.simulatedBasicsStations.Count / numberOfFactories;

            // The total number of concentratos can be configured via the test configuration. It will de distributed evenly among factories;
            // The total number of devices can be configured via the test configuration. It will de distributed evenly among factories.

            // Devices are distributed across different LNS within the same factory to "increase resiliency". In the case of two LNS and one factory,
            // this means that for two concentrators, concentrator 1 will be connected to LNS 1 while concentrator 2 will talk to LNS 2.
            // This influences how many messages we expect in IoT Hub (due to different concentrator/LNS deduplication strategies).

            Assert.True(this.simulatedBasicsStations.Count >= numberOfFactories, "There needs to be at least one concentrator per factory.");
            Assert.True(stationsPerFactory % Configuration.LnsEndpointsForSimulator.Count == 0, "LNS must be distributed evenly across factories (identical amount of indirectly connected LNS to factories).");

            var testDeviceInfo = TestFixtureSim.DeviceRange9000_OTAA_FullLoad_DuplicationDrop;
            var devicesAndConcentratorsByFactory =
                this.simulatedBasicsStations.Batch(stationsPerFactory)
                                            .Take(numberOfFactories)
                                            .Zip(testDeviceInfo.Batch(testDeviceInfo.Count / numberOfFactories).Take(numberOfFactories))
                                            .Select(b => (Stations: b.First.ToList(), DeviceInfo: b.Second))
                                            .Select(b => (b.Stations, Devices: b.DeviceInfo.Select(d => InitializeSimulatedDevice(d, b.Stations)).ToList()))
                                            .Index()
                                            .ToDictionary(el => el.Key,
                                                          stationsAndDevices => (stationsAndDevices.Value.Stations, stationsAndDevices.Value.Devices));
            // Cache the devices in a flat list to make distributing requests easier.
            // Transposing the matrix makes sure that device requests are distributed evenly accross factories,
            // instead of having all requests from the same factory executed in series.
            var devices =
                devicesAndConcentratorsByFactory.Select(f => f.Value.Devices)
                                                .Transpose()
                                                .SelectMany(ds => ds)
                                                .ToList();

            var stopwatch = Stopwatch.StartNew();

            // Join OTAA devices
            this.logger.LogInformation("Starting join phase at {Timestamp}.", DateTime.UtcNow);
            await Task.WhenAll(from taskAndOffset in DistributeEvenly(devices, joinsPerSecond)
                               select JoinAsync(taskAndOffset.Element, taskAndOffset.Offset));

            static async Task JoinAsync(SimulatedDevice simulatedDevice, TimeSpan offset)
            {
                await Task.Delay(offset);
                Assert.True(await simulatedDevice.JoinAsync(), $"OTAA join for device {simulatedDevice.LoRaDevice.DeviceID} failed.");
            }

            // Send messages
            foreach (var (index, messageRate) in messagesPerSecond.Take(numberOfLoops).Index())
            {
                this.logger.LogInformation("Running cycle {Cycle} of {TotalNumberOfCycles} at {Timestamp}.", index + 1, numberOfLoops, DateTime.UtcNow);
                await Task.WhenAll(from taskAndOffset in DistributeEvenly(devices, messageRate)
                                   select SendUpstreamAsync(taskAndOffset.Element, taskAndOffset.Offset));
            }

            async Task SendUpstreamAsync(SimulatedDevice simulatedDevice, TimeSpan offset)
            {
                await Task.Delay(offset);
                using var request = CreateConfirmedUpstreamMessage(simulatedDevice);
                await simulatedDevice.SendDataMessageAsync(request);
            }

            stopwatch.Stop();
            this.logger.LogInformation("Sent {NumberOfMessages} messages in {Seconds} seconds.", (numberOfLoops + 1) * devices.Count, stopwatch.Elapsed.TotalSeconds);

            await WaitForResultsInIotHubAsync();

            var actualMessageCounts = new Dictionary<string, int>();
            foreach (var device in devices)
            {
                actualMessageCounts.Add(device.LoRaDevice.DeviceID, TestFixture.IoTHubMessages.Events.Count(e => ContainsMessageFromDevice(e, device)));
            }

            this.logger.LogInformation("Message counts by device ID:");
            this.logger.LogInformation(JsonSerializer.Serialize(actualMessageCounts));

            foreach (var (i, device) in devices.Index())
            {
                this.logger.LogInformation("Asserting device {DeviceId} ({Index}/{Total})", device.LoRaDevice.DeviceID, i + 1, devices.Count);
                // A correction needs to be applied since concentrators are distributed across LNS, even if they are in the same factory
                // (detailed description found at the beginning of this test).
                var expectedMessageCorrection = 1 / (double)numberOfFactories;
                var actualCount = actualMessageCounts[device.LoRaDevice.DeviceID];
                var expectedCount = GetExpectedMessageCount(device.LoRaDevice.Deduplication, numberOfLoops) * expectedMessageCorrection;
                Assert.True(expectedCount <= actualCount, $"Actual count was {actualCount} but expected at least {expectedCount}.");
                EnsureMessageResponsesAreReceived(device, numberOfLoops + 1);
            }

            static IEnumerable<(T Element, TimeSpan Offset)> DistributeEvenly<T>(ICollection<T> input, double rate)
            {
                var stepTime = TimeSpan.FromSeconds(1) / rate;
                return input.Index().Select(el => (el.Value, el.Key * stepTime));
            }
        }

        // Scenario:
        // - ABP devices
        // - OTAA devices
        // - Sending confirmed/unconfirmed messages
        // - Goal: N devices in parallel based on configuration
        [Fact]
        public async Task Multiple_ABP_and_OTAA_Simulated_Devices_Confirmed()
        {
            const int messagesPerDeviceExcludingWarmup = 10;
            const int batchSizeDataMessages = 15;
            const int batchSizeWarmupMessages = 2;
            const int messagesBeforeJoin = 5;
            const int messagesBeforeConfirmed = 5;
            var warmupDelay = TimeSpan.FromSeconds(5);

            var simulatedAbpDevices = InitializeSimulatedDevices(TestFixtureSim.DeviceRange2000_ABP_FullLoad);
            var simulatedOtaaDevices = InitializeSimulatedDevices(TestFixtureSim.DeviceRange3000_OTAA_FullLoad);
            Assert.Equal(simulatedAbpDevices.Count, simulatedOtaaDevices.Count);
            Assert.True(simulatedOtaaDevices.Count < 50, "Simulator does not work for more than 50 of each devices (due to IoT Edge connection mode). To go beyond 100 device clients, use edge hub environment variable 'MaxConnectedClients'.");
            Assert.True(messagesBeforeConfirmed <= messagesBeforeJoin, "OTAA devices should send all messages as confirmed messages.");

            // 1. picking devices send an initial message (warm device cache in LNS module)
            foreach (var devices in simulatedAbpDevices.Batch(batchSizeWarmupMessages))
            {
                await Task.WhenAll(from device in devices select SendWarmupMessageAsync(device));
                await Task.Delay(warmupDelay);
            }

            async Task SendWarmupMessageAsync(SimulatedDevice device)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(10, 250)));
                this.logger.LogInformation("[WARM-UP] {DeviceId}", device.LoRaDevice.DeviceID);
                // warm-up messages should not contain the message fragment (we do not want to include them in the message count)
                using var request = WaitableLoRaRequest.CreateWaitableRequest(device.CreateConfirmedDataUpMessage("foo"));
                await device.SendDataMessageAsync(request);
            }

            // 2. ABP and OTAA devices send messages
            const int initialMessageId = 1;
            for (var messageId = initialMessageId; messageId < initialMessageId + messagesPerDeviceExcludingWarmup; ++messageId)
            {
                foreach (var batch in simulatedAbpDevices.Zip(simulatedOtaaDevices, (first, second) => (Abp: first, Otaa: second))
                                                         .Batch(batchSizeDataMessages))
                {
                    var payload = this.uniqueMessageFragment + messageId.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0');
                    var abpTasks = batch.Select(devices => SendUpstreamMessage(devices.Abp, payload, messageId));
                    var otaaTasks = messageId switch
                    {
                        messagesBeforeJoin + initialMessageId => batch.Select(devices => JoinAsync(devices.Otaa)),
                        > messagesBeforeJoin + initialMessageId => batch.Select(devices => SendUpstreamMessage(devices.Otaa, payload, messageId)),
                        _ => Array.Empty<Task>()
                    };

                    await Task.WhenAll(abpTasks.Concat(otaaTasks));
                    await Task.Delay(IntervalBetweenMessages);
                }
            }

            static async Task SendUpstreamMessage(SimulatedDevice device, string payload, int messageId)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(10, 250)));

                using var waitableLoRaRequest = messageId <= initialMessageId + messagesBeforeConfirmed
                    ? WaitableLoRaRequest.CreateWaitableRequest(device.CreateUnconfirmedDataUpMessage(payload))
                    : WaitableLoRaRequest.CreateWaitableRequest(device.CreateConfirmedDataUpMessage(payload));

                await device.SendDataMessageAsync(waitableLoRaRequest);
            }

            static async Task JoinAsync(SimulatedDevice device)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(10, 5000)));
                Assert.True(await device.JoinAsync(), "OTAA join failed");
            }

            await WaitForResultsInIotHubAsync();

            // 3. Check that the correct number of messages have arrived in IoT Hub per device
            //    Warn only.
            foreach (var device in simulatedAbpDevices)
            {
                Assert.Equal(GetExpectedMessageCount(device.LoRaDevice.Deduplication, messagesPerDeviceExcludingWarmup), TestFixture.IoTHubMessages.Events.Count(e => ContainsMessageFromDevice(e, device)));
                EnsureMessageResponsesAreReceived(device, messagesPerDeviceExcludingWarmup - messagesBeforeConfirmed);
            }
            foreach (var device in simulatedOtaaDevices)
            {
                // number of total data messages is number of messages per device minus the join message minus the number of messages sent before the join happens.
                const int numberOfOtaaDataMessages = messagesPerDeviceExcludingWarmup - messagesBeforeJoin - 1;
                Assert.Equal(GetExpectedMessageCount(device.LoRaDevice.Deduplication, numberOfOtaaDataMessages), TestFixture.IoTHubMessages.Events.Count(e => ContainsMessageFromDevice(e, device)));
                EnsureMessageResponsesAreReceived(device, numberOfOtaaDataMessages + 1);
            }
        }

        private static void EnsureMessageResponsesAreReceived(SimulatedDevice device, int expectedCount)
        {
            if (expectedCount == 0) throw new ArgumentException(null, nameof(expectedCount));
            var minimumMessagesReceived = Math.Max((int)(expectedCount * (1 - DownstreamDroppedMessagesTolerance)), 1);
            Assert.True(minimumMessagesReceived <= device.ReceivedMessages.Count, $"Too many downlink messages were dropped. Received {device.ReceivedMessages.Count} messages but expected at least {minimumMessagesReceived}.");
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

        /// <summary>
        /// Requests between LNS are always deduplicated, while duplicate station requests are deduplicated based on the deduplication strategy.
        /// </summary>
        private int GetExpectedMessageCount(string deduplicationMode, int numberOfMessagesPerDevice) =>
            deduplicationMode?.ToUpperInvariant() switch
            {
                null or "" or "NONE" => numberOfMessagesPerDevice * this.simulatedBasicsStations.Count,
                "MARK" => numberOfMessagesPerDevice * this.simulatedBasicsStations.Count,
                "DROP" => numberOfMessagesPerDevice,
                _ => throw new NotImplementedException()
            };

        private WaitableLoRaRequest CreateConfirmedUpstreamMessage(SimulatedDevice simulatedDevice) =>
            WaitableLoRaRequest.CreateWaitableRequest(simulatedDevice.CreateConfirmedDataUpMessage(this.uniqueMessageFragment + Guid.NewGuid()));

        private static Task WaitForResultsInIotHubAsync() => Task.Delay(TimeSpan.FromMinutes(5));

        private bool ContainsMessageFromDevice(EventData eventData, SimulatedDevice simulatedDevice)
        {
            if (eventData.Properties.ContainsKey("iothub-message-schema")) return false;
            if (eventData.GetDeviceId() != simulatedDevice.LoRaDevice.DeviceID) return false;
            return Encoding.UTF8.GetString(eventData.Body).Contains(this.uniqueMessageFragment, StringComparison.Ordinal);
        }

        private List<SimulatedDevice> InitializeSimulatedDevices(IReadOnlyCollection<TestDeviceInfo> testDeviceInfos) =>
            testDeviceInfos.Select(d => InitializeSimulatedDevice(d, this.simulatedBasicsStations)).ToList();

        private SimulatedDevice InitializeSimulatedDevice(TestDeviceInfo testDeviceInfo, IReadOnlyCollection<SimulatedBasicsStation> simulatedBasicsStations) =>
            new SimulatedDevice(testDeviceInfo, simulatedBasicsStation: simulatedBasicsStations, logger: this.logger);

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
                    await basicsStation.StopAndValidateAsync();
                    basicsStation.Dispose();
                }
                catch (Exception)
                {
                    // Dispose all basics stations
                }
            }
        }
    }
}
