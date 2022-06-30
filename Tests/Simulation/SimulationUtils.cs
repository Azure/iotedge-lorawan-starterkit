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
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging;
    using Xunit;

    internal class SimulationUtils
    {
        private const double DownstreamDroppedMessagesTolerance = 0.02;

        internal static void AssertMessageAcknowledgement(SimulatedDevice device, int expectedCount) =>
            AssertMessageAcknowledgements(new[] { device }, expectedCount);

        internal static void AssertMessageAcknowledgements(IEnumerable<SimulatedDevice> devices, int expectedCount)
        {
            if (expectedCount == 0) throw new ArgumentException(null, nameof(expectedCount));

            foreach (var device in devices)
            {
                var minimumMessagesReceived = Math.Max((int)(expectedCount * (1 - DownstreamDroppedMessagesTolerance)), 1);
                Assert.True(minimumMessagesReceived <= device.ReceivedMessages.Count, $"Too many downlink messages were dropped. Received {device.ReceivedMessages.Count} messages but expected at least {minimumMessagesReceived}.");
            }
        }

        internal static async Task SendConfirmedUpstreamMessages(SimulatedDevice device, int count, string uniqueMessageFragment, int intervalBetweenMessagesInSeconds = 5)
        {
            for (var i = 0; i < count; ++i)
            {
                using var request = CreateConfirmedUpstreamMessage(device, uniqueMessageFragment);
                await device.SendDataMessageAsync(request);
                await Task.Delay(TimeSpan.FromSeconds(intervalBetweenMessagesInSeconds));
            }
        }

        internal static WaitableLoRaRequest CreateConfirmedUpstreamMessage(SimulatedDevice simulatedDevice, string uniqueMessageFragment) =>
            WaitableLoRaRequest.CreateWaitableRequest(simulatedDevice.CreateConfirmedDataUpMessage(uniqueMessageFragment + Guid.NewGuid()));

        internal static Task AssertIotHubMessageCountAsync(SimulatedDevice device,
                                                           int numberOfMessages,
                                                           string uniqueMessageFragment,
                                                           TestOutputLogger logger,
                                                           int basicStationCount,
                                                           EventHubDataCollector hubDataCollector,
                                                           int lnsEndpointsForSimulatorCount) =>
            AssertIotHubMessageCountsAsync(new[] { device },
                                           numberOfMessages,
                                           uniqueMessageFragment,
                                           logger,
                                           basicStationCount,
                                           hubDataCollector,
                                           lnsEndpointsForSimulatorCount);

        internal static async Task AssertIotHubMessageCountsAsync(IEnumerable<SimulatedDevice> devices,
                                                                  int numberOfMessages,
                                                                  string uniqueMessageFragment,
                                                                  TestOutputLogger logger,
                                                                  int basicStationCount,
                                                                  EventHubDataCollector hubDataCollector,
                                                                  int lnsEndpointsForSimulatorCount,
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
                actualMessageCounts.Add(device.DevEUI, hubDataCollector.Events.Count(e => ContainsMessageFromDevice(e, device)));
            }

            bool ContainsMessageFromDevice(EventData eventData, SimulatedDevice simulatedDevice)
            {
                if (eventData.Properties.ContainsKey("iothub-message-schema")) return false;
                if (eventData.GetDeviceId() != simulatedDevice.LoRaDevice.DeviceID) return false;
                return Encoding.UTF8.GetString(eventData.EventBody).Contains(uniqueMessageFragment, StringComparison.Ordinal);
            }

            logger.Log(LogLevel.Information, "Message counts by DevEui:");
            logger.Log(LogLevel.Information, JsonSerializer.Serialize(actualMessageCounts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)));

            foreach (var device in devices)
            {
                var expectedMessageCount = device.LoRaDevice.Deduplication switch
                {
                    DeduplicationMode.None or DeduplicationMode.Mark => numberOfMessages * basicStationCount,
                    DeduplicationMode.Drop => numberOfMessages,
                    var mode => throw new SwitchExpressionException(mode)
                };

                if (!string.IsNullOrEmpty(device.LoRaDevice.GatewayID))
                {
                    expectedMessageCount /= lnsEndpointsForSimulatorCount;
                }

                var applicableMessageCount = correction is { } someCorrection ? expectedMessageCount * someCorrection : expectedMessageCount;
                var actualMessageCount = actualMessageCounts[device.DevEUI];
                // Takes into account at-least-once delivery guarantees.
                Assert.True(applicableMessageCount <= actualMessageCount, $"Expected at least {applicableMessageCount} IoT Hub messages for device {device.DevEUI} but counted {actualMessageCount}.");
            }
        }

        internal static List<SimulatedDevice> InitializeSimulatedDevices(IReadOnlyCollection<TestDeviceInfo> testDeviceInfos,
                                                                         IReadOnlyCollection<SimulatedBasicsStation> simulatedBasicsStations,
                                                                         TestOutputLogger logger) =>
            testDeviceInfos.Select(d => InitializeSimulatedDevice(d, simulatedBasicsStations, logger)).ToList();

        internal static SimulatedDevice InitializeSimulatedDevice(TestDeviceInfo testDeviceInfo,
                                                                 IReadOnlyCollection<SimulatedBasicsStation> simulatedBasicsStations,
                                                                 TestOutputLogger logger) =>
            new SimulatedDevice(testDeviceInfo, simulatedBasicsStation: simulatedBasicsStations, logger: logger);
    }
}
