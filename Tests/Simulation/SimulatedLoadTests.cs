// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Extensions.Logging;
    using Xunit;
    using Xunit.Abstractions;
    using static MoreLinq.Extensions.BatchExtension;

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
            var device = new SimulatedDevice(TestFixtureSim.Device1001_Simulated_ABP, simulatedBasicsStation: this.simulatedBasicsStations, logger: this.logger);

            await SendConfirmedUpstreamMessages(device, messageCount);
            await WaitForResultsInIotHubAsync();

            Assert.Equal(GetExpectedMessageCount(device.LoRaDevice.Deduplication, messageCount), TestFixture.IoTHubMessages.Events.Count(eventData => ContainsMessageFromDevice(eventData, device)));
            device.EnsureMessageResponsesAreReceived(messageCount);
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

        // Scenario:
        // - ABP devices
        // - OTAA devices
        // - Sending confirmed/unconfirmed messages
        // - Goal: N devices in parallel based on configuration
        [Fact]
        public async Task Multiple_ABP_and_OTAA_Simulated_Devices_Confirmed()
        {
            // amount of messages to send per device (without warm-up phase)
            const int scenarioMessagesPerDevice = 10;

            // amount of devices to send data in parallel
            const int scenarioDeviceStepSize = 15;

            // amount of devices to send data in parallel for the warm-up phase
            const int warmUpDeviceStepSize = 2;

            // amount of messages to send before device join is to occur
            const int messagesBeforeJoin = 5;

            // amount of Unconfirmed messges to send before Confirmed message is to occur
            const int messagesBeforeConfirmed = 5;

            // amount of seconds to wait between sends in warmup phase
            var delayWarmup = TimeSpan.FromSeconds(5);

            var simulatedAbpDevices = InitializeSimulatedDevices(TestFixtureSim.DeviceRange2000_ABP_FullLoad);
            var simulatedOtaaDevices = InitializeSimulatedDevices(TestFixtureSim.DeviceRange3000_OTAA_FullLoad);
            Assert.Equal(simulatedAbpDevices.Count, simulatedOtaaDevices.Count);
            Assert.True(simulatedOtaaDevices.Count < 50, "Simulator does not work for more than 50 of each devices (due to IoT Edge connection mode). To go beyond 100 device clients, use edge hub environment variable 'MaxConnectedClients'.");
            Assert.True(messagesBeforeConfirmed <= messagesBeforeJoin, "OTAA devices should send all messages as confirmed messages.");

            // 1. picking devices send an initial message (warm device cache in LNS module)
            foreach (var devices in simulatedAbpDevices.Batch(warmUpDeviceStepSize))
            {
                await Task.WhenAll(from device in devices select SendWarmupMessageAsync(device));
                await Task.Delay(delayWarmup);
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
            for (var messageId = initialMessageId; messageId < initialMessageId + scenarioMessagesPerDevice; ++messageId)
            {
                foreach (var batch in simulatedAbpDevices.Zip(simulatedOtaaDevices, (first, second) => (Abp: first, Otaa: second))
                                                         .Batch(scenarioDeviceStepSize))
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
            }

            await WaitForResultsInIotHubAsync();

            // 3. Check that the correct number of messages have arrived in IoT Hub per device
            //    Warn only.
            foreach (var device in simulatedAbpDevices)
            {
                Assert.Equal(GetExpectedMessageCount(device.LoRaDevice.Deduplication, scenarioMessagesPerDevice), TestFixture.IoTHubMessages.Events.Count(e => ContainsMessageFromDevice(e, device)));
                device.EnsureMessageResponsesAreReceived(scenarioMessagesPerDevice - messagesBeforeConfirmed);
            }
            foreach (var device in simulatedOtaaDevices)
            {
                // number of total data messages is number of messages per device minus the join message minus the number of messages sent before the join happens.
                const int numberOfOtaaDataMessages = scenarioMessagesPerDevice - messagesBeforeJoin - 1;
                Assert.Equal(GetExpectedMessageCount(device.LoRaDevice.Deduplication, numberOfOtaaDataMessages), TestFixture.IoTHubMessages.Events.Count(e => ContainsMessageFromDevice(e, device)));
                device.EnsureMessageResponsesAreReceived(numberOfOtaaDataMessages + 1);
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
            testDeviceInfos.Select(d => new SimulatedDevice(d, simulatedBasicsStation: this.simulatedBasicsStations, logger: this.logger)).ToList();

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
