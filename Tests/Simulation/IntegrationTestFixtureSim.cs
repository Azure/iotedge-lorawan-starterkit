// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Simulation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
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

        private readonly List<TestDeviceInfo> deviceRange2000_ABP_FullLoad = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange2000_ABP_FullLoad => this.deviceRange2000_ABP_FullLoad;

        private readonly List<TestDeviceInfo> deviceRange3000_OTAA_FullLoad = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange3000_OTAA_FullLoad => this.deviceRange3000_OTAA_FullLoad;

        private readonly List<TestDeviceInfo> deviceRange4000_OTAA_FullLoad = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange4000_OTAA_FullLoad => this.deviceRange4000_OTAA_FullLoad;

        private readonly List<TestDeviceInfo> deviceRange5000_BasicsStationSimulators = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange5000_BasicsStationSimulators => this.deviceRange5000_BasicsStationSimulators;

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

            for (var deviceID = 5000; deviceID <= 5000 + NumberOfConcentrators - 1; deviceID++)
            {
                this.deviceRange5000_BasicsStationSimulators.Add(new TestDeviceInfo
                {
                    DeviceID = deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture),
                    RouterConfig = JObject.Parse(jsonString),
                    IsIoTHubDevice = true,
                });
            }

            for (var deviceId = 1100; deviceId <= 1110; deviceId++)
                this.deviceRange1000_ABP.Add(CreateAbpDevice(deviceId));

            for (var deviceId = 2000; deviceId <= 2000 + Configuration.NumberOfLoadTestDevices; deviceId++)
                this.deviceRange2000_ABP_FullLoad.Add(CreateAbpDevice(deviceId));

            for (var deviceId = 3000; deviceId <= 3000 + Configuration.NumberOfLoadTestDevices; deviceId++)
                this.deviceRange3000_OTAA_FullLoad.Add(CreateOtaaDevice(deviceId));

            for (var deviceId = 4000; deviceId < 4000 + Configuration.NumberOfLoadTestDevices; deviceId++)
                this.deviceRange4000_OTAA_FullLoad.Add(CreateOtaaDevice(deviceId));

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

            TestDeviceInfo CreateOtaaDevice(int deviceId) =>
                new TestDeviceInfo
                {
                    DeviceID = deviceId.ToString("0000000000000000", CultureInfo.InvariantCulture),
                    AppEui = JoinEui.Parse(deviceId.ToString("0000000000000000", CultureInfo.InvariantCulture)),
                    AppKey = GetAppKey(deviceId),
                    IsIoTHubDevice = true,
                    SensorDecoder = "DecoderValueSensor",
                };
        }

        private AppSessionKey GetAppSessionKey(int value) => AppSessionKey.Parse(GetKeyString(value));
        private NetworkSessionKey GetNetworkSessionKey(int value) => NetworkSessionKey.Parse(GetKeyString(value));
        private AppKey GetAppKey(int value) => AppKey.Parse(GetKeyString(value));

        private string GetKeyString(int value) => GetKey32(value);
    }
}
