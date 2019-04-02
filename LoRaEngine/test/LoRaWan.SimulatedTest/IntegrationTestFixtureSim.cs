// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.SimulatedTest
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaWan.Test.Shared;

    public class IntegrationTestFixtureSim : IntegrationTestFixtureBase
    {
        // Device1001_Simulated_ABP: used for ABP simulator
        public TestDeviceInfo Device1001_Simulated_ABP { get; private set; }

        // Device1002_Simulated_OTAA: used for simulator
        public TestDeviceInfo Device1002_Simulated_OTAA { get; private set; }

        // Device1003_Simulated_HttpBasedDecoder: used for simulator http based decoding test
        public TestDeviceInfo Device1003_Simulated_HttpBasedDecoder { get; private set; }

        List<TestDeviceInfo> deviceRange1000_ABP = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange1000_ABP
        {
            get { return this.deviceRange1000_ABP; }
        }

        List<TestDeviceInfo> deviceRange2000_1000_ABP = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange2000_1000_ABP
        {
            get { return this.deviceRange2000_1000_ABP; }
        }

        List<TestDeviceInfo> deviceRange3000_10_OTAA = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange3000_10_OTAA
        {
            get { return this.deviceRange3000_10_OTAA; }
        }

        public override void SetupTestDevices()
        {
            string gatewayID = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID") ?? this.Configuration.LeafDeviceGatewayID;

            // Simulated devices start at 1000

            // Device1001_Simulated_ABP: used for ABP simulator
            this.Device1001_Simulated_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000001001",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = "00000000000000000000000000001001",
                NwkSKey = "00000000000000000000000000001001",
                DevAddr = "00001001",
            };

            // Device1002_Simulated_OTAA: used for simulator
            this.Device1002_Simulated_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000001002",
                AppEUI = "0000000000001002",
                AppKey = "00000000000000000000000000001002",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            // Device1003_Simulated_HttpBasedDecoder: used for simulator http based decoding test
            this.Device1003_Simulated_HttpBasedDecoder = new TestDeviceInfo
            {
                DeviceID = "0000000000001003",
                AppEUI = "0000000000001003",
                AppKey = "00000000000000000000000000001003",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "http://localhost:8888/api/DecoderValueSensor",
            };

            for (int deviceID = 1100; deviceID <= 1110; deviceID++)
            {
                this.deviceRange1000_ABP.Add(
                    new TestDeviceInfo
                    {
                        DeviceID = deviceID.ToString("0000000000000000"),
                        AppEUI = deviceID.ToString("0000000000000000"),
                        AppKey = deviceID.ToString("00000000000000000000000000000000"),
                        GatewayID = gatewayID,
                        IsIoTHubDevice = true,
                        SensorDecoder = "DecoderValueSensor",
                        AppSKey = deviceID.ToString("00000000000000000000000000000000"),
                        NwkSKey = deviceID.ToString("00000000000000000000000000000000"),
                        DevAddr = deviceID.ToString("00000000"),
                    });
            }

            // Range of 1000 ABP devices from 2000 to 2999: Used for load testing
            for (int deviceID = 2000; deviceID <= 2999; deviceID++)
            {
                this.deviceRange2000_1000_ABP.Add(
                    new TestDeviceInfo
                    {
                        DeviceID = deviceID.ToString("0000000000000000"),
                        GatewayID = gatewayID,
                        IsIoTHubDevice = true,
                        SensorDecoder = "DecoderValueSensor",
                        KeepAliveTimeout = 0,
                        AppSKey = deviceID.ToString("00000000000000000000000000000000"),
                        NwkSKey = deviceID.ToString("00000000000000000000000000000000"),
                        DevAddr = deviceID.ToString("00000000"),
                    });
            }

            // Range of 10 OTAA devices from 3000 to 3009: Used for load testing
            for (int deviceID = 3000; deviceID <= 3009; deviceID++)
            {
                this.deviceRange3000_10_OTAA.Add(
                    new TestDeviceInfo
                    {
                        DeviceID = deviceID.ToString("0000000000000000"),
                        AppEUI = deviceID.ToString("0000000000000000"),
                        AppKey = deviceID.ToString("00000000000000000000000000000000"),
                        GatewayID = gatewayID,
                        IsIoTHubDevice = true,
                        SensorDecoder = "DecoderValueSensor",
                    });
            }
        }
    }
}
