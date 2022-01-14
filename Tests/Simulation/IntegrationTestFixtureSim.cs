// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;
    using LoRaWan.Tests.Common;
    using Newtonsoft.Json.Linq;

    public class IntegrationTestFixtureSim : IntegrationTestFixtureBase
    {
        private const int NumberOfConcentrators = 2;
        // Device1001_Simulated_ABP: used for ABP simulator
        public TestDeviceInfo Device1001_Simulated_ABP { get; private set; }

        // Device1002_Simulated_OTAA: used for simulator
        public TestDeviceInfo Device1002_Simulated_OTAA { get; private set; }

        // Device1003_Simulated_HttpBasedDecoder: used for simulator http based decoding test
        public TestDeviceInfo Device1003_Simulated_HttpBasedDecoder { get; private set; }

        private readonly List<TestDeviceInfo> deviceRange1000_ABP = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange1000_ABP => this.deviceRange1000_ABP;

        private readonly List<TestDeviceInfo> deviceRange2000_1000_ABP = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange2000_1000_ABP => this.deviceRange2000_1000_ABP;

        private readonly List<TestDeviceInfo> deviceRange3000_10_OTAA = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange3000_10_OTAA => this.deviceRange3000_10_OTAA;

        private readonly List<TestDeviceInfo> deviceRange4000_500_OTAA = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange4000_500_OTAA => this.deviceRange4000_500_OTAA;

        private readonly List<TestDeviceInfo> deviceRange5000_BasicsStationSimulators = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange5000_BasicsStationSimulators => this.deviceRange5000_BasicsStationSimulators;

        private readonly List<SimulatedBasicsStation> simulatedBasicsStations = new List<SimulatedBasicsStation>();
        public IReadOnlyCollection<SimulatedBasicsStation> SimulatedBasicsStations => this.simulatedBasicsStations;

        public override async Task InitializeDevicesAsync()
        {
            var index = 0;
            var startTasks = new List<Task>();
            foreach (var basicsStationDevice in DeviceRange5000_BasicsStationSimulators)
            {
                var simulatedBasicsStation = new SimulatedBasicsStation(StationEui.Parse(basicsStationDevice.DeviceID), new Uri(Configuration.LNSEndpointsForSimulator[index % Configuration.LNSEndpointsForSimulator.Count]));
                startTasks.Add(simulatedBasicsStation.StartAsync());
                simulatedBasicsStations.Add(simulatedBasicsStation);
            }

            await Task.WhenAll(startTasks);
        }

        public override void SetupTestDevices()
        {
            var gatewayID = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID") ?? Configuration.LeafDeviceGatewayID;

            // Simulated devices start at 1000

            // Device1001_Simulated_ABP: used for ABP simulator
            Device1001_Simulated_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000001001",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = GetAppSessionKey(1001),
                NwkSKey = GetNetworkSessionKey(1001),
                DevAddr = new DevAddr(0x00001001),
            };

            // Device1002_Simulated_OTAA: used for simulator
            Device1002_Simulated_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000001002",
                AppEui = JoinEui.Parse("0000000000001002"),
                AppKey = GetAppKey(1002),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            // Device1003_Simulated_HttpBasedDecoder: used for simulator http based decoding test
            Device1003_Simulated_HttpBasedDecoder = new TestDeviceInfo
            {
                DeviceID = "0000000000001003",
                AppEui = JoinEui.Parse("0000000000001003"),
                AppKey = GetAppKey(1003),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "http://localhost:8888/api/DecoderValueSensor",
            };

            var fileName = "EU863.json";
            var jsonString = File.ReadAllText(fileName);

            for (var deviceID = 5000; deviceID <= 5000 + NumberOfConcentrators - 1 ; deviceID++)
            {
                this.deviceRange5000_BasicsStationSimulators.Add(new TestDeviceInfo
                {
                    DeviceID = deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture),
                    RouterConfig = JObject.Parse(jsonString),
                    IsIoTHubDevice = true,
                });
            }

            for (var deviceID = 1100; deviceID <= 1110; deviceID++)
            {
                this.deviceRange1000_ABP.Add(
                    new TestDeviceInfo
                    {
                        DeviceID = deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture),
                        AppEui = JoinEui.Parse(deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture)),
                        AppKey = GetAppKey(deviceID),
                        GatewayID = gatewayID,
                        IsIoTHubDevice = true,
                        SensorDecoder = "DecoderValueSensor",
                        AppSKey = GetAppSessionKey(deviceID),
                        NwkSKey = GetNetworkSessionKey(deviceID),
                        DevAddr = DevAddr.Parse(deviceID.ToString("00000000", CultureInfo.InvariantCulture)),
                    });
            }

            for (var deviceID = 4000; deviceID < 4500; deviceID++)
            {
                this.deviceRange4000_500_OTAA.Add(
                    new TestDeviceInfo
                    {
                        DeviceID = deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture),
                        AppEui = JoinEui.Parse(deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture)),
                        AppKey = GetAppKey(deviceID),
                        GatewayID = gatewayID,
                        IsIoTHubDevice = true,
                        SensorDecoder = "DecoderValueSensor",
                    });
            }

            /*
            // Range of 1000 ABP devices from 2000 to 2999: Used for load testing
            for (var deviceID = 2000; deviceID <= 2999; deviceID++)
            {
                this.deviceRange2000_1000_ABP.Add(
                    new TestDeviceInfo
                    {
                        DeviceID = deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture),
                        GatewayID = gatewayID,
                        IsIoTHubDevice = true,
                        SensorDecoder = "DecoderValueSensor",
                        KeepAliveTimeout = 0,
                        AppSKey = GetAppSessionKey(deviceID),
                        NwkSKey = GetNetworkSessionKey(deviceID),
                        DevAddr = DevAddr.Parse(deviceID.ToString("00000000", CultureInfo.InvariantCulture)),
                    });
            }

            // Range of 10 OTAA devices from 3000 to 3009: Used for load testing
            for (var deviceID = 3000; deviceID <= 3009; deviceID++)
            {
                this.deviceRange3000_10_OTAA.Add(
                    new TestDeviceInfo
                    {
                        DeviceID = deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture),
                        AppEui = JoinEui.Parse(deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture)),
                        AppKey = GetAppKey(deviceID),
                        GatewayID = gatewayID,
                        IsIoTHubDevice = true,
                        SensorDecoder = "DecoderValueSensor",
                    });
            }
            */
        }

        private AppSessionKey GetAppSessionKey(int value) => AppSessionKey.Parse(GetKeyString(value));
        private NetworkSessionKey GetNetworkSessionKey(int value) => NetworkSessionKey.Parse(GetKeyString(value));
        private AppKey GetAppKey(int value) => AppKey.Parse(GetKeyString(value));

        private string GetKeyString(int value) => GetKey32(value);

        public override async Task DisposeAsync()
        {
            foreach (var basicsStation in simulatedBasicsStations)
            {
                await basicsStation.StopAsync();
                basicsStation.Dispose();
            }
        }
    }
}
