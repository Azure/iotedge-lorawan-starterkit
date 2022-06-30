// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Newtonsoft.Json.Linq;

    public class IntegrationTestFixtureSim : IntegrationTestFixtureBase
    {
        // Device1001_Simulated_ABP: used for ABP simulator
        public TestDeviceInfo Device1001_Simulated_ABP { get; private set; }

        // Device1002_Simulated_OTAA: used for simulator
        public TestDeviceInfo Device1002_Simulated_OTAA { get; private set; }

        // Device1003_Simulated_ABP: used for ABP simulator
        public TestDeviceInfo Device1003_Simulated_ABP { get; private set; }

        // Device1004_Simulated_ABP: used for ABP simulator
        public TestDeviceInfo Device1004_Simulated_ABP { get; private set; }

        private readonly List<TestDeviceInfo> deviceRange1000_ABP = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange1000_ABP => this.deviceRange1000_ABP;

        private readonly List<TestDeviceInfo> deviceRange2000_ABP_FullLoad = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange2000_ABP_FullLoad => this.deviceRange2000_ABP_FullLoad;

        private readonly List<TestDeviceInfo> deviceRange3000_OTAA_FullLoad = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange3000_OTAA_FullLoad => this.deviceRange3000_OTAA_FullLoad;

        private readonly List<TestDeviceInfo> deviceRange4000_OTAA_FullLoad = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange4000_OTAA_FullLoad => this.deviceRange4000_OTAA_FullLoad;

        private readonly List<TestDeviceInfo> deviceRange5000_BasicsStationSimulators = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange5000_BasicsStationSimulators => this.deviceRange5000_BasicsStationSimulators;

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange6000_OTAA_FullLoad { get; private set; }
        public IReadOnlyCollection<TestDeviceInfo> DeviceRange9000_OTAA_FullLoad_DuplicationDrop { get; private set; }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            LoRaAPIHelper.Initialize(Configuration.FunctionAppCode, Configuration.FunctionAppBaseUrl);
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

            // Device1003_Simulated_ABP: used for simulator
            Device1003_Simulated_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000001003",
                Deduplication = DeduplicationMode.Drop,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = GetAppSessionKey(1003),
                NwkSKey = GetNetworkSessionKey(1003),
                DevAddr = new DevAddr(0x00001003),
            };

            // Device1004_Simulated_ABP: used for simulator
            Device1004_Simulated_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000001004",
                Deduplication = DeduplicationMode.Drop,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = GetAppSessionKey(1004),
                NwkSKey = GetNetworkSessionKey(1004),
                DevAddr = new DevAddr(0x00001004),
                ClassType = LoRaDeviceClassType.C
            };

            var fileName = "EU863.json";
            var jsonString = File.ReadAllText(fileName);

            for (var deviceID = 5000; deviceID < 5000 + Configuration.NumberOfLoadTestConcentrators; deviceID++)
            {
                this.deviceRange5000_BasicsStationSimulators.Add(new TestDeviceInfo
                {
                    DeviceID = deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture),
                    RouterConfig = JObject.Parse(jsonString),
                    IsIoTHubDevice = true,
                });
            }

            for (var deviceId = 1100; deviceId < 1105; deviceId++)
                this.deviceRange1000_ABP.Add(CreateAbpDevice(deviceId));

            for (var deviceId = 2000; deviceId < 2000 + Configuration.NumberOfLoadTestDevices; deviceId++)
                this.deviceRange2000_ABP_FullLoad.Add(CreateAbpDevice(deviceId));

            for (var deviceId = 3000; deviceId < 3000 + Configuration.NumberOfLoadTestDevices; deviceId++)
                this.deviceRange3000_OTAA_FullLoad.Add(CreateOtaaDevice(deviceId));

            for (var deviceId = 4000; deviceId < 4000 + Configuration.NumberOfLoadTestDevices; deviceId++)
                this.deviceRange4000_OTAA_FullLoad.Add(CreateOtaaDevice(deviceId));

            DeviceRange6000_OTAA_FullLoad =
                Enumerable.Range(6000, Configuration.NumberOfLoadTestDevices)
                          .Select(deviceId => CreateOtaaDevice(deviceId))
                          .ToList();

            DeviceRange9000_OTAA_FullLoad_DuplicationDrop =
                Enumerable.Range(9000, Configuration.NumberOfLoadTestDevices)
                          .Select(deviceId => CreateOtaaDevice(deviceId, deduplicationMode: DeduplicationMode.Drop))
                          .ToList();

            TestDeviceInfo CreateAbpDevice(int deviceId) =>
                new TestDeviceInfo
                {
                    DeviceID = deviceId.ToString("0000000000000000", CultureInfo.InvariantCulture),
                    AppEui = JoinEui.Parse(deviceId.ToString("0000000000000000", CultureInfo.InvariantCulture)),
                    AppKey = GetAppKey(deviceId),
                    GatewayID = gatewayID,
                    IsIoTHubDevice = true,
                    SensorDecoder = "DecoderValueSensor",
                    AppSKey = GetAppSessionKey(deviceId),
                    NwkSKey = GetNetworkSessionKey(deviceId),
                    DevAddr = DevAddr.Parse(deviceId.ToString("00000000", CultureInfo.InvariantCulture)),
                };

            TestDeviceInfo CreateOtaaDevice(int deviceId, DeduplicationMode deduplicationMode = DeduplicationMode.Drop) =>
                new TestDeviceInfo
                {
                    DeviceID = deviceId.ToString("0000000000000000", CultureInfo.InvariantCulture),
                    AppEui = JoinEui.Parse(deviceId.ToString("0000000000000000", CultureInfo.InvariantCulture)),
                    AppKey = GetAppKey(deviceId),
                    IsIoTHubDevice = true,
                    SensorDecoder = "DecoderValueSensor",
                    Deduplication = deduplicationMode
                };
        }

        private AppSessionKey GetAppSessionKey(int value) => AppSessionKey.Parse(GetKeyString(value));
        private NetworkSessionKey GetNetworkSessionKey(int value) => NetworkSessionKey.Parse(GetKeyString(value));
        private AppKey GetAppKey(int value) => AppKey.Parse(GetKeyString(value));

        private string GetKeyString(int value) => GetKey32(value);
    }
}
