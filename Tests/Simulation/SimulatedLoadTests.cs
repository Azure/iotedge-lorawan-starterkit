// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Azure.Messaging.EventHubs;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging;
    using NetworkServer;
    using Xunit;
    using Xunit.Abstractions;
    using static MoreLinq.Extensions.RepeatExtension;
    using static MoreLinq.Extensions.IndexExtension;
    using static MoreLinq.Extensions.TransposeExtension;
    using LoRaWan.NetworkServer.BasicsStation.ModuleConnection;

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
                testFixture.DeviceRange5000_BasicsStationSimulators
                           .Zip(Configuration.LnsEndpointsForSimulator.Repeat(),
                                (tdi, lnsNameToUrl) => new SimulatedBasicsStation(StationEui.Parse(tdi.DeviceID), lnsNameToUrl.Value))
                           .ToList();

            Assert.True(this.simulatedBasicsStations.Count % Configuration.LnsEndpointsForSimulator.Count == 0, "Since Basics Stations are round-robin distributed to LNS, we must have the same number of stations per LNS for well-defined test assertions.");
        }

        [Fact]
        public async Task Five_Devices_Sending_Messages_At_Same_Time()
        {
            // arrange
            const int messageCount = 2;
            var simulatedDevices = InitializeSimulatedDevices(TestFixtureSim.DeviceRange1000_ABP);
            Assert.NotEmpty(simulatedDevices);

            // act
            await Task.WhenAll(from device in simulatedDevices
                               select SendConfirmedUpstreamMessages(device, messageCount));

            // assert
            await AssertIotHubMessageCountsAsync(simulatedDevices, messageCount);
            AssertMessageAcknowledgements(simulatedDevices, messageCount);
        }

        [Fact]
        public async Task Single_ABP_Simulated_Device()
        {
            const int messageCount = 5;
            var device = new SimulatedDevice(TestFixtureSim.Device1001_Simulated_ABP, simulatedBasicsStation: this.simulatedBasicsStations, logger: this.logger);

            await SendConfirmedUpstreamMessages(device, messageCount);

            await AssertIotHubMessageCountAsync(device, messageCount);
            AssertMessageAcknowledgement(device, messageCount);
        }

        [Fact]
        public async Task Ensures_Disconnect_Happens_For_Losing_Gateway_When_Connection_Switches()
        {
            // arrange
            var device = new SimulatedDevice(TestFixtureSim.Device1003_Simulated_ABP, simulatedBasicsStation: new[] { this.simulatedBasicsStations.First() }, logger: this.logger);
            await SendConfirmedUpstreamMessages(device, 1);

            // act: change basics station that the device is listened from and therefore the gateway it uses as well
            device.SimulatedBasicsStations = new[] { this.simulatedBasicsStations.Last() };
            await SendConfirmedUpstreamMessages(device, 1);

            // assert
            var expectedLnsToDropConnection = Configuration.LnsEndpointsForSimulator.First().Key;
            await TestFixture.AssertNetworkServerModuleLogExistsAsync(
                x => x.Contains(ModuleConnectionHost.DroppedConnectionLog, StringComparison.Ordinal) && x.Contains(expectedLnsToDropConnection, StringComparison.Ordinal),
                new SearchLogOptions($"{ModuleConnectionHost.DroppedConnectionLog} and {expectedLnsToDropConnection}") { TreatAsError = true });
            await AssertIotHubMessageCountAsync(device, 2);
        }

        [Fact]
        public async Task Single_OTAA_Simulated_Device()
        {
            const int messageCount = 5;
            var device = new SimulatedDevice(TestFixtureSim.Device1002_Simulated_OTAA, simulatedBasicsStation: this.simulatedBasicsStations, logger: this.logger);

            Assert.True(await device.JoinAsync(), "OTAA join failed");
            await SendConfirmedUpstreamMessages(device, messageCount);

            await AssertIotHubMessageCountAsync(device, messageCount);
            AssertMessageAcknowledgement(device, messageCount + 1);
        }

        [Fact]
        public async Task Lots_Of_Devices_OTAA_Simulated_Load_Test()
        {
            // arrange
            const int messageCounts = 10;
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

            // assert
            await AssertIotHubMessageCountsAsync(simulatedDevices, messageCounts);
            AssertMessageAcknowledgements(simulatedDevices, messageCounts + 1);
        }

        /// <summary>
        /// This test emulates a scenario where we have several plants with multiple concentrators and devices,
        /// connected to several gateways (either on-premises or in the cloud).
        /// </summary>
        [Fact]
        public async Task Connected_Factory_Load_Test_Scenario()
        {
            const int numberOfFactories = 2;
            const double joinsPerSecond = 1.5;
            var messagesPerSecond = MoreLinq.MoreEnumerable.Generate(1.5, old => old > 2 ? old : old + 2);
            const int numberOfLoops = 5;
            var stationsPerFactory = this.simulatedBasicsStations.Count / numberOfFactories;

            // The total number of concentrators can be configured via the test configuration. It will de distributed evenly among factories;
            // The total number of devices can be configured via the test configuration. It will de distributed evenly among factories.

            // Devices are distributed across different LNS within the same factory to "increase resiliency". In the case of two LNS and one factory,
            // this means that for two concentrators, concentrator 1 will be connected to LNS 1 while concentrator 2 will talk to LNS 2.
            // This influences how many messages we expect in IoT Hub (due to different concentrator/LNS deduplication strategies).

            Assert.True(stationsPerFactory >= 1, "There needs to be at least one concentrator per factory.");
            Assert.True(stationsPerFactory % Configuration.LnsEndpointsForSimulator.Count == 0, "LNS must be distributed evenly across factories (identical amount of indirectly connected LNS to factories).");

            var testDeviceInfo = TestFixtureSim.DeviceRange9000_OTAA_FullLoad_DuplicationDrop;
            var devicesByFactory =
                this.simulatedBasicsStations.Chunk(stationsPerFactory)
                                            .Take(numberOfFactories)
                                            .Zip(testDeviceInfo.Chunk(testDeviceInfo.Count / numberOfFactories)
                                                               .Take(numberOfFactories),
                                                 (ss, ds) => ds.Select(d => InitializeSimulatedDevice(d, ss)).ToList());

            // Cache the devices in a flat list to make distributing requests easier.
            // Transposing the matrix makes sure that device requests are distributed evenly across factories,
            // instead of having all requests from the same factory executed in series.
            var devices = devicesByFactory.Transpose().SelectMany(ds => ds).ToList();

            var stopwatch = Stopwatch.StartNew();

            // Join OTAA devices
            this.logger.LogInformation("Starting join phase at {Timestamp}.", DateTime.UtcNow);

            await ScheduleForEachAsync(devices, Intervals(TimeSpan.FromSeconds(1) / joinsPerSecond),
                                       async d => Assert.True(await d.JoinAsync(), $"OTAA join for device {d.LoRaDevice.DeviceID} failed."));

            // Send messages
            foreach (var (cycle, messageRate) in messagesPerSecond.Take(numberOfLoops).Index(1))
            {
                this.logger.LogInformation("Running cycle {Cycle} of {TotalNumberOfCycles} at {Timestamp}.", cycle, numberOfLoops, DateTime.UtcNow);

                await ScheduleForEachAsync(devices, Intervals(TimeSpan.FromSeconds(1) / messageRate),
                                           async d =>
                                           {
                                               using var request = CreateConfirmedUpstreamMessage(d);
                                               await d.SendDataMessageAsync(request);
                                           });
            }

            stopwatch.Stop();
            this.logger.LogInformation("Sent {NumberOfMessages} messages in {Seconds} seconds.", (numberOfLoops + 1) * devices.Count, stopwatch.Elapsed.TotalSeconds);

            // A correction needs to be applied since concentrators are distributed across LNS, even if they are in the same factory
            // (detailed description found at the beginning of this test).
            await AssertIotHubMessageCountsAsync(devices, numberOfLoops, 1 / (double)numberOfFactories);
            AssertMessageAcknowledgements(devices, numberOfLoops + 1);

            static IEnumerable<TimeSpan> Intervals(TimeSpan step, TimeSpan? initial = null) =>
                MoreLinq.MoreEnumerable.Generate(initial ?? TimeSpan.Zero, ts => ts + step);

            static Task ScheduleForEachAsync<T>(IEnumerable<T> input,
                                                IEnumerable<TimeSpan> intervals,
                                                Func<T, Task> action) =>
                Task.WhenAll(input.Zip(intervals, async (e, d) => { await Task.Delay(d); await action(e); }));
        }

        // Scenario:
        // - ABP devices
        // - OTAA devices
        // - Sending confirmed/unconfirmed messages
        // - Goal: N devices in parallel based on configuration
        [Fact(Skip = "Test is only used for manual load tests.")]
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
            foreach (var devices in simulatedAbpDevices.Chunk(batchSizeWarmupMessages))
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
                                                         .Chunk(batchSizeDataMessages))
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

            // 3. Check that the correct number of messages have arrived in IoT Hub per device
            //    Warn only.
            await AssertIotHubMessageCountsAsync(simulatedAbpDevices, messagesPerDeviceExcludingWarmup);
            AssertMessageAcknowledgements(simulatedAbpDevices, messagesPerDeviceExcludingWarmup - messagesBeforeConfirmed);

            // number of total data messages is number of messages per device minus the join message minus the number of messages sent before the join happens.
            const int numberOfOtaaDataMessages = messagesPerDeviceExcludingWarmup - messagesBeforeJoin - 1;
            await AssertIotHubMessageCountsAsync(simulatedOtaaDevices, numberOfOtaaDataMessages, disableWaitForIotHub: true);
            AssertMessageAcknowledgements(simulatedOtaaDevices, numberOfOtaaDataMessages + 1);
        }

        private static void AssertMessageAcknowledgement(SimulatedDevice device, int expectedCount) =>
            AssertMessageAcknowledgements(new[] { device }, expectedCount);

        private static void AssertMessageAcknowledgements(IEnumerable<SimulatedDevice> devices, int expectedCount)
        {
            if (expectedCount == 0) throw new ArgumentException(null, nameof(expectedCount));

            foreach (var device in devices)
            {
                var minimumMessagesReceived = Math.Max((int)(expectedCount * (1 - DownstreamDroppedMessagesTolerance)), 1);
                Assert.True(minimumMessagesReceived <= device.ReceivedMessages.Count, $"Too many downlink messages were dropped. Received {device.ReceivedMessages.Count} messages but expected at least {minimumMessagesReceived}.");
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

        private WaitableLoRaRequest CreateConfirmedUpstreamMessage(SimulatedDevice simulatedDevice) =>
            WaitableLoRaRequest.CreateWaitableRequest(simulatedDevice.CreateConfirmedDataUpMessage(this.uniqueMessageFragment + Guid.NewGuid()));

        private Task AssertIotHubMessageCountAsync(SimulatedDevice device, int numberOfMessages) =>
            AssertIotHubMessageCountsAsync(new[] { device }, numberOfMessages);

        private async Task AssertIotHubMessageCountsAsync(IEnumerable<SimulatedDevice> devices,
                                                          int numberOfMessages,
                                                          double? correction = null,
                                                          bool disableWaitForIotHub = false)
        {
            // Wait for messages in IoT Hub.
            if (!disableWaitForIotHub)
            {
                await Task.Delay(TimeSpan.FromSeconds(100));
            }

            var actualMessageCounts = new Dictionary<DevEui, int>();
            foreach (var device in devices)
            {
                actualMessageCounts.Add(device.DevEUI, TestFixture.IoTHubMessages.Events.Count(e => ContainsMessageFromDevice(e, device)));
            }

            bool ContainsMessageFromDevice(EventData eventData, SimulatedDevice simulatedDevice)
            {
                if (eventData.Properties.ContainsKey("iothub-message-schema")) return false;
                if (eventData.GetDeviceId() != simulatedDevice.LoRaDevice.DeviceID) return false;
                return Encoding.UTF8.GetString(eventData.EventBody).Contains(this.uniqueMessageFragment, StringComparison.Ordinal);
            }

            this.logger.LogInformation("Message counts by DevEui:");
            this.logger.LogInformation(JsonSerializer.Serialize(actualMessageCounts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)));

            foreach (var device in devices)
            {
                var expectedMessageCount = device.LoRaDevice.Deduplication switch
                {
                    DeduplicationMode.None or DeduplicationMode.Mark => numberOfMessages * this.simulatedBasicsStations.Count,
                    DeduplicationMode.Drop => numberOfMessages,
                    var mode => throw new SwitchExpressionException(mode)
                };

                if (!string.IsNullOrEmpty(device.LoRaDevice.GatewayID))
                {
                    expectedMessageCount /= Configuration.LnsEndpointsForSimulator.Count;
                }

                var applicableMessageCount = correction is { } someCorrection ? expectedMessageCount * someCorrection : expectedMessageCount;
                var actualMessageCount = actualMessageCounts[device.DevEUI];
                // Takes into account at-least-once delivery guarantees.
                Assert.True(applicableMessageCount <= actualMessageCount, $"Expected at least {applicableMessageCount} IoT Hub messages for device {device.DevEUI} but counted {actualMessageCount}.");
            }
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
