// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
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

    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class SimulatedCloudTests : IntegrationTestBaseSim, IAsyncLifetime
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

        public SimulatedCloudTests(IntegrationTestFixtureSim testFixture, ITestOutputHelper testOutputHelper)
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
        public async Task Single_ABP_Simulated_Device()
        {
            var testDeviceInfo = TestFixtureSim.Device1004_Simulated_ABP;
            LogTestStart(testDeviceInfo);

            const int messageCount = 5;
            var device = new SimulatedDevice(testDeviceInfo, simulatedBasicsStation: this.simulatedBasicsStations, logger: this.logger);

            await SendConfirmedUpstreamMessages(device, messageCount);

            await AssertIotHubMessageCountAsync(device, messageCount);
            AssertMessageAcknowledgement(device, messageCount);
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
