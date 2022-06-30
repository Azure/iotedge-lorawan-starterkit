// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaWan.Tests.Common;
    using Xunit;
    using Xunit.Abstractions;
    using static MoreLinq.Extensions.RepeatExtension;

    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class SimulatedCloudTests : IntegrationTestBaseSim, IAsyncLifetime
    {
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
        public async Task Single_ABP_Simulated_Device_Sends_And_Receives_C2D()
        {
            var testDeviceInfo = TestFixtureSim.Device1004_Simulated_ABP;
            LogTestStart(testDeviceInfo);

            const int messageCount = 5;
            var device = new SimulatedDevice(testDeviceInfo, simulatedBasicsStation: this.simulatedBasicsStations, logger: this.logger);

            await SimulationUtils.SendConfirmedUpstreamMessages(device, messageCount, this.uniqueMessageFragment);

            await SimulationUtils.AssertIotHubMessageCountAsync(device,
                                                                messageCount,
                                                                this.uniqueMessageFragment,
                                                                this.logger,
                                                                this.simulatedBasicsStations.Count,
                                                                TestFixture.IoTHubMessages,
                                                                Configuration.LnsEndpointsForSimulator.Count);

            var c2dMessageBody = (100 + Random.Shared.Next(90)).ToString(CultureInfo.InvariantCulture);
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = FramePorts.App1,
                MessageId = Guid.NewGuid().ToString(),
            };

            await TestFixture.SendCloudToDeviceMessageAsync(device.LoRaDevice.DeviceID, c2dMessage);
            Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var c2dLogMessage = $"{device.LoRaDevice.DeviceID}: done processing 'Complete' on cloud to device message, id: '{c2dMessage.MessageId}'";
            Log($"Expected C2D network server log is: {c2dLogMessage}");

            await SimulationUtils.SendConfirmedUpstreamMessages(device, messageCount, this.uniqueMessageFragment);

            var searchResults = await TestFixture.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(c2dLogMessage, StringComparison.OrdinalIgnoreCase);
                    },
                    new SearchLogOptions(c2dLogMessage)
                    {
                        MaxAttempts = 1
                    });

            Assert.True(searchResults.Found, $"Did not find '{device.LoRaDevice.DeviceID}: C2D log: {c2dLogMessage}' in logs");

            SimulationUtils.AssertMessageAcknowledgement(device, messageCount);
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
