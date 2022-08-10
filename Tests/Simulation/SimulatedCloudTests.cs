// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaWan.Tests.Common;
    using Newtonsoft.Json;
    using Xunit;
    using Xunit.Abstractions;
    using static MoreLinq.Extensions.RepeatExtension;

    [Trait("Category", "SkipWhenLiveUnitTesting")]
#pragma warning disable xUnit1033 // False positive: Test classes decorated with 'Xunit.IClassFixture<TFixture>' or 'Xunit.ICollectionFixture<TFixture>' should add a constructor argument of type TFixture
    public sealed class SimulatedCloudTests : IntegrationTestBaseSim, IAsyncLifetime
#pragma warning restore xUnit1033 // False positive: Test classes decorated with 'Xunit.IClassFixture<TFixture>' or 'Xunit.ICollectionFixture<TFixture>' should add a constructor argument of type TFixture
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

            TestLogger.Log($"[INFO] Simulating send of {messageCount} messages from {device.LoRaDevice.DeviceID}");
            await SimulationUtils.SendConfirmedUpstreamMessages(device, messageCount, this.uniqueMessageFragment);

            // Now sending a c2d
            var c2d = new LoRaCloudToDeviceMessage()
            {
                DevEUI = device.DevEUI,
                MessageId = Guid.NewGuid().ToString(),
                Fport = FramePorts.App23,
                RawPayload = Convert.ToBase64String(new byte[] { 0xFF, 0x00 }),
            };

            TestLogger.Log($"[INFO] Using service API to send C2D message to device {device.LoRaDevice.DeviceID}");
            TestLogger.Log($"[INFO] {JsonConvert.SerializeObject(c2d, Formatting.None)}");

            // send message using the SendCloudToDeviceMessage API endpoint
            Assert.True(await LoRaAPIHelper.SendCloudToDeviceMessage(device.DevEUI, c2d));

            var c2dLogMessage = $"{device.LoRaDevice.DeviceID}: received cloud to device message from direct method";
            TestLogger.Log($"[INFO] Searching for following log in LNS logs: '{c2dLogMessage}'");

            var searchResults = await TestFixture.SearchNetworkServerModuleAsync(
                    messageBody => messageBody.StartsWith(c2dLogMessage, StringComparison.OrdinalIgnoreCase),
                    new SearchLogOptions(c2dLogMessage)
                    {
                        MaxAttempts = 1
                    });

            Assert.True(searchResults.Found, $"Did not find '{device.LoRaDevice.DeviceID}: C2D log: {c2dLogMessage}' in logs");

            TestLogger.Log($"[INFO] Asserting all messages were received in IoT Hub for device {device.LoRaDevice.DeviceID}");
            await SimulationUtils.AssertIotHubMessageCountAsync(device,
                                                                messageCount,
                                                                this.uniqueMessageFragment,
                                                                this.logger,
                                                                this.simulatedBasicsStations.Count,
                                                                TestFixture.IoTHubMessages,
                                                                Configuration.LnsEndpointsForSimulator.Count);
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
