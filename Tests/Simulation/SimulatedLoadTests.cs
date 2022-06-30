// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging;
    using NetworkServer;
    using Xunit;
    using Xunit.Abstractions;
    using static MoreLinq.Extensions.RepeatExtension;
    using static MoreLinq.Extensions.IndexExtension;
    using static MoreLinq.Extensions.TransposeExtension;

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
            var testDeviceInfo = TestFixtureSim.DeviceRange1000_ABP;
            LogTestStart(testDeviceInfo);

            // arrange
            const int messageCount = 2;
            var simulatedDevices = SimulationUtils.InitializeSimulatedDevices(testDeviceInfo, this.simulatedBasicsStations, logger);
            Assert.NotEmpty(simulatedDevices);

            // act
            await Task.WhenAll(from device in simulatedDevices
                               select SimulationUtils.SendConfirmedUpstreamMessages(device, messageCount, this.uniqueMessageFragment));

            // assert
            await SimulationUtils.AssertIotHubMessageCountsAsync(simulatedDevices,
                                                                 messageCount,
                                                                 this.uniqueMessageFragment,
                                                                 this.logger,
                                                                 this.simulatedBasicsStations.Count,
                                                                 TestFixture.IoTHubMessages,
                                                                 Configuration.LnsEndpointsForSimulator.Count);
            SimulationUtils.AssertMessageAcknowledgements(simulatedDevices, messageCount);
        }

        [Fact]
        public async Task Single_ABP_Simulated_Device()
        {
            var testDeviceInfo = TestFixtureSim.Device1001_Simulated_ABP;
            LogTestStart(testDeviceInfo);

            const int messageCount = 5;
            var device = new SimulatedDevice(testDeviceInfo, simulatedBasicsStation: this.simulatedBasicsStations, logger: this.logger);

            await SimulationUtils.SendConfirmedUpstreamMessages(device, messageCount, this.uniqueMessageFragment);

            await SimulationUtils.AssertIotHubMessageCountAsync(device, messageCount, this.uniqueMessageFragment, this.logger, this.simulatedBasicsStations.Count, TestFixture.IoTHubMessages, Configuration.LnsEndpointsForSimulator.Count);
            SimulationUtils.AssertMessageAcknowledgement(device, messageCount);
        }

        [Fact]
        public async Task Ensures_Disconnect_Happens_For_Losing_Gateway_When_Connection_Switches()
        {
            // arrange
            var testDeviceInfo = TestFixtureSim.Device1003_Simulated_ABP;
            LogTestStart(testDeviceInfo);

            var messagesToSendEachLNS = 3;
            var simulatedDevice = new SimulatedDevice(testDeviceInfo, simulatedBasicsStation: new[] { this.simulatedBasicsStations.First() }, logger: this.logger);
            await SimulationUtils.SendConfirmedUpstreamMessages(simulatedDevice, messagesToSendEachLNS, this.uniqueMessageFragment);

            await Task.Delay(messagesToSendEachLNS * IntervalBetweenMessages);
            _ = await TestFixture.AssertNetworkServerModuleLogExistsAsync(
                x => !x.Contains(LnsRemoteCallHandler.ClosedConnectionLog, StringComparison.Ordinal),
                new SearchLogOptions("No connection switch should be logged") { TreatAsError = true });

            // act: change basics station that the device is listened from and therefore the gateway it uses as well
            simulatedDevice.SimulatedBasicsStations = new[] { this.simulatedBasicsStations.Last() };
            await SimulationUtils.SendConfirmedUpstreamMessages(simulatedDevice, messagesToSendEachLNS, this.uniqueMessageFragment);

            // assert
            var expectedLnsToDropConnection = Configuration.LnsEndpointsForSimulator.First().Key;
            _ = await TestFixture.AssertNetworkServerModuleLogExistsAsync(
                x => x.Contains(LnsRemoteCallHandler.ClosedConnectionLog, StringComparison.Ordinal) && x.Contains(expectedLnsToDropConnection, StringComparison.Ordinal),
                new SearchLogOptions($"{LnsRemoteCallHandler.ClosedConnectionLog} and {expectedLnsToDropConnection}") { TreatAsError = true });
            await SimulationUtils.AssertIotHubMessageCountAsync(simulatedDevice, messagesToSendEachLNS * 2, this.uniqueMessageFragment, this.logger, this.simulatedBasicsStations.Count, TestFixture.IoTHubMessages, Configuration.LnsEndpointsForSimulator.Count);
        }

        [Fact]
        public async Task Single_OTAA_Simulated_Device()
        {
            var testDeviceInfo = TestFixtureSim.Device1002_Simulated_OTAA;
            LogTestStart(testDeviceInfo);

            const int messageCount = 5;
            var device = new SimulatedDevice(testDeviceInfo, simulatedBasicsStation: this.simulatedBasicsStations, logger: this.logger);

            Assert.True(await device.JoinAsync(), "OTAA join failed");
            await SimulationUtils.SendConfirmedUpstreamMessages(device, messageCount, this.uniqueMessageFragment);

            await SimulationUtils.AssertIotHubMessageCountAsync(device, messageCount, this.uniqueMessageFragment, this.logger, this.simulatedBasicsStations.Count, TestFixture.IoTHubMessages, Configuration.LnsEndpointsForSimulator.Count);
            SimulationUtils.AssertMessageAcknowledgement(device, messageCount + 1);
        }

        [Fact]
        public async Task Lots_Of_Devices_OTAA_Simulated_Load_Test()
        {
            var testDeviceInfo = TestFixtureSim.DeviceRange4000_OTAA_FullLoad;
            LogTestStart(testDeviceInfo);

            // arrange
            const int messageCounts = 10;
            var simulatedDevices = SimulationUtils.InitializeSimulatedDevices(testDeviceInfo, this.simulatedBasicsStations, this.logger);
            Assert.NotEmpty(simulatedDevices);

            // act
            var offsetInterval = IntervalBetweenMessages / simulatedDevices.Count;
            await Task.WhenAll(from deviceWithOffset in simulatedDevices.Select((device, i) => (Device: device, Offset: i * offsetInterval))
                               select ActAsync(deviceWithOffset.Device, deviceWithOffset.Offset));

            async Task ActAsync(SimulatedDevice device, TimeSpan startOffset)
            {
                await Task.Delay(startOffset);
                Assert.True(await device.JoinAsync(), "OTAA join failed");
                await SimulationUtils.SendConfirmedUpstreamMessages(device, messageCounts, this.uniqueMessageFragment);
            }

            // assert
            await SimulationUtils.AssertIotHubMessageCountsAsync(simulatedDevices, messageCounts, this.uniqueMessageFragment, this.logger, this.simulatedBasicsStations.Count, TestFixture.IoTHubMessages, Configuration.LnsEndpointsForSimulator.Count);
            SimulationUtils.AssertMessageAcknowledgements(simulatedDevices, messageCounts + 1);
        }

        /// <summary>
        /// This test emulates a scenario where we have several plants with multiple concentrators and devices,
        /// connected to several gateways (either on-premises or in the cloud).
        /// </summary>
        [Fact]
        public async Task Connected_Factory_Load_Test_Scenario()
        {
            var testDeviceInfo = TestFixtureSim.DeviceRange9000_OTAA_FullLoad_DuplicationDrop;
            LogTestStart(testDeviceInfo);

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

            var devicesByFactory =
                this.simulatedBasicsStations.Chunk(stationsPerFactory)
                                            .Take(numberOfFactories)
                                            .Zip(testDeviceInfo.Chunk(testDeviceInfo.Count / numberOfFactories)
                                                               .Take(numberOfFactories),
                                                 (ss, ds) => ds.Select(d => SimulationUtils.InitializeSimulatedDevice(d, ss, this.logger)).ToList());

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
                                               using var request = SimulationUtils.CreateConfirmedUpstreamMessage(d, this.uniqueMessageFragment);
                                               await d.SendDataMessageAsync(request);
                                           });
            }

            stopwatch.Stop();
            this.logger.LogInformation("Sent {NumberOfMessages} messages in {Seconds} seconds.", (numberOfLoops + 1) * devices.Count, stopwatch.Elapsed.TotalSeconds);

            // A correction needs to be applied since concentrators are distributed across LNS, even if they are in the same factory
            // (detailed description found at the beginning of this test).
            await SimulationUtils.AssertIotHubMessageCountsAsync(devices, numberOfLoops, this.uniqueMessageFragment, this.logger, this.simulatedBasicsStations.Count, TestFixture.IoTHubMessages, Configuration.LnsEndpointsForSimulator.Count, 1 / (double)numberOfFactories);
            SimulationUtils.AssertMessageAcknowledgements(devices, numberOfLoops + 1);

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
            var testAbpDevicesInfo = TestFixtureSim.DeviceRange2000_ABP_FullLoad;
            var testOtaaDevicesInfo = TestFixtureSim.DeviceRange3000_OTAA_FullLoad;
            LogTestStart(testAbpDevicesInfo.Concat(testOtaaDevicesInfo));
            const int messagesPerDeviceExcludingWarmup = 10;
            const int batchSizeDataMessages = 15;
            const int batchSizeWarmupMessages = 2;
            const int messagesBeforeJoin = 5;
            const int messagesBeforeConfirmed = 5;
            var warmupDelay = TimeSpan.FromSeconds(5);

            var simulatedAbpDevices = SimulationUtils.InitializeSimulatedDevices(testAbpDevicesInfo, this.simulatedBasicsStations, this.logger);
            var simulatedOtaaDevices = SimulationUtils.InitializeSimulatedDevices(testOtaaDevicesInfo, this.simulatedBasicsStations, this.logger);
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
            await SimulationUtils.AssertIotHubMessageCountsAsync(simulatedAbpDevices, messagesPerDeviceExcludingWarmup, this.uniqueMessageFragment, this.logger, this.simulatedBasicsStations.Count, TestFixture.IoTHubMessages, Configuration.LnsEndpointsForSimulator.Count);
            SimulationUtils.AssertMessageAcknowledgements(simulatedAbpDevices, messagesPerDeviceExcludingWarmup - messagesBeforeConfirmed);

            // number of total data messages is number of messages per device minus the join message minus the number of messages sent before the join happens.
            const int numberOfOtaaDataMessages = messagesPerDeviceExcludingWarmup - messagesBeforeJoin - 1;
            await SimulationUtils.AssertIotHubMessageCountsAsync(simulatedOtaaDevices, numberOfOtaaDataMessages, this.uniqueMessageFragment, this.logger, this.simulatedBasicsStations.Count, TestFixture.IoTHubMessages, Configuration.LnsEndpointsForSimulator.Count, disableWaitForIotHub: true);
            SimulationUtils.AssertMessageAcknowledgements(simulatedOtaaDevices, numberOfOtaaDataMessages + 1);
        }

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
