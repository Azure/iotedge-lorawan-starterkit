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
            const int scenarioDeviceStepSize = 20;

            // amount of devices to send data in parallel for the warm-up phase
            const int warmUpDeviceStepSize = 2;

            // amount of messages to send before device join is to occur
            const int messagesBeforeJoin = 10;

            // amount of Unconfirmed messges to send before Confirmed message is to occur
            const int messagesBeforeConfirmed = 5;

            // amount of seconds to wait between sends in warmup phase
            var delayWarmup = TimeSpan.FromSeconds(5);

            var simulatedAbpDevices = InitializeSimulatedDevices(TestFixtureSim.DeviceRange2000_ABP_FullLoad);
            var simulatedOtaaDevices = InitializeSimulatedDevices(TestFixtureSim.DeviceRange3000_OTAA_FullLoad);

            // 1. picking devices send an initial message (warm device cache in LNS module)
            for (var i = 0; i < simulatedAbpDevices.Count; i += warmUpDeviceStepSize)
            {
                await Task.WhenAll(from device in simulatedAbpDevices.Skip(i).Take(warmUpDeviceStepSize)
                                   select SendWarmupMessageAsync(device));
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

            // 3. ABP and OTAA devices send messages
            const int initialMessageId = 1;
            for (var messageId = initialMessageId; messageId <= scenarioMessagesPerDevice; ++messageId)
            {
                for (var i = 0; i < simulatedAbpDevices.Count; i += scenarioDeviceStepSize)
                {
                    var payload = GetPayloadForMessageId(messageId);
                    var abpTasks =
                        simulatedAbpDevices.Skip(i).Take(scenarioDeviceStepSize)
                                           .Select(device => SendUpstreamMessage(device, payload, messageId));

                    static async Task SendUpstreamMessage(SimulatedDevice device, string payload, int messageId)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(10, 250)));

                        using var waitableLoRaRequest = messageId <= initialMessageId + messagesBeforeConfirmed
                            ? WaitableLoRaRequest.CreateWaitableRequest(device.CreateUnconfirmedDataUpMessage(payload))
                            : WaitableLoRaRequest.CreateWaitableRequest(device.CreateConfirmedDataUpMessage(payload));

                        await device.SendDataMessageAsync(waitableLoRaRequest);
                    }

                    IEnumerable<Task> otaaTasks = new List<Task>();
                    if (messageId == messagesBeforeJoin + initialMessageId)
                    {
                        otaaTasks =
                            from device in simulatedOtaaDevices
                            select JoinAsync(device);

                        // Join all devices over a range of 5 seconds.
                        async Task JoinAsync(SimulatedDevice device)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(10, 5000)));
                            Assert.True(await device.JoinAsync(), "OTAA join failed");
                            this.logger.LogInformation("Join request sent for {DeviceId}", device.LoRaDevice.DeviceID);
                        }
                    }
                    else if (messageId > messagesBeforeJoin + initialMessageId)
                    {
                        otaaTasks =
                            from device in simulatedOtaaDevices
                            select SendUpstreamMessage(device, payload, messageId);
                    }

                    await Task.WhenAll(abpTasks.Concat(otaaTasks));
                    await Task.Delay(IntervalBetweenMessages);
                }
            }

            await WaitForResultsInIotHubAsync();

            // 6. Check that the correct number of messages have arrived in IoT Hub per device
            //    Warn only.
            foreach (var device in simulatedAbpDevices)
            {
                Assert.Equal(GetExpectedMessageCount(device.LoRaDevice.Deduplication, scenarioMessagesPerDevice), TestFixture.IoTHubMessages.Events.Count(e => ContainsMessageFromDevice(e, device)));
                device.EnsureMessageResponsesAreReceived(scenarioMessagesPerDevice - messagesBeforeConfirmed);
            }
            foreach (var device in simulatedOtaaDevices)
            {
                Assert.Equal(GetExpectedMessageCount(device.LoRaDevice.Deduplication, scenarioMessagesPerDevice), TestFixture.IoTHubMessages.Events.Count(e => ContainsMessageFromDevice(e, device)));
                device.EnsureMessageResponsesAreReceived(scenarioMessagesPerDevice - messagesBeforeConfirmed + 1);
            }

            // 7. Check if all expected messages have arrived in IoT Hub
            //    Warn only.
            foreach (var device in simulatedAbpDevices)
            {
                this.logger.LogInformation("Looking for IoT Hub messages for '{DeviceId}'", device.LoRaDevice.DeviceID);
                for (var messageId = 0; messageId <= scenarioMessagesPerDevice; ++messageId)
                {
                    // Find message containing '{"value":<seed>.<0 to number of msg/device>}' for all leaf devices in IoT Hub
                    var expectedPayload = $"{{\"value\":{GetPayloadForMessageId(messageId)}}}";
                    await TestFixture.AssertIoTHubDeviceMessageExistsAsync(device.LoRaDevice.DeviceID, expectedPayload);
                }
            }

            string GetPayloadForMessageId(int messageId) => this.uniqueMessageFragment + messageId.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0');
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
    }
}
