// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaWan.Test.Shared;

    public class IntegrationTestFixtureCi : IntegrationTestFixtureBase, IDisposable
    {
        // Device1_OTAA: used for join test only
        public TestDeviceInfo Device1_OTAA { get; private set; }

        // Device2_OTAA: used for failed join (wrong devEUI)
        public TestDeviceInfo Device2_OTAA { get; private set; }

        // Device3_OTAA: used for failed join (wrong appKey)
        public TestDeviceInfo Device3_OTAA { get; private set; }

        // Device4_OTAA: used for OTAA confirmed & unconfirmed messaging
        public TestDeviceInfo Device4_OTAA { get; private set; }

        // Device5_ABP: used for ABP confirmed & unconfirmed messaging
        public TestDeviceInfo Device5_ABP { get; private set; }

        // Device6_ABP: used for ABP wrong devaddr
        public TestDeviceInfo Device6_ABP { get; private set; }

        // Device7_ABP: used for ABP wrong nwkskey
        public TestDeviceInfo Device7_ABP { get; private set; }

        // Device8_ABP: used for ABP invalid nwkskey (mic fails)
        public TestDeviceInfo Device8_ABP { get; private set; }

        // Device9_OTAA: used for OTAA confirmed messages, C2D test
        public TestDeviceInfo Device9_OTAA { get; private set; }

        // Device10_OTAA: used for OTAA unconfirmed messages, C2D test
        public TestDeviceInfo Device10_OTAA { get; private set; }

        // Device11_OTAA: used for http decoder
        public TestDeviceInfo Device11_OTAA { get; private set; }

        // Device12_OTAA: used for reflection based decoder
        public TestDeviceInfo Device12_OTAA { get; private set; }

        // Device13_OTAA: used for wrong AppEUI OTAA join
        public TestDeviceInfo Device13_OTAA { get; private set; }

        // Device14_OTAA: used for test confirmed C2D
        public TestDeviceInfo Device14_OTAA { get; private set; }

        // Device15_OTAA: used for test fport C2D
        public TestDeviceInfo Device15_OTAA { get; private set; }

        // Device16_ABP: used for test on multiple device with same devaddr
        public TestDeviceInfo Device16_ABP { get; private set; }

        // Device17_ABP: used for test on multiple device with same devaddr
        public TestDeviceInfo Device17_ABP { get; private set; }

        // Device18_ABP: used for C2D invalid fport testing
        public TestDeviceInfo Device18_ABP { get; private set; }

        // Device19_ABP: used for C2D invalid fport testing
        public TestDeviceInfo Device19_ABP { get; private set; }

        // Device20_OTAA: used for OTAA confirmed & unconfirmed messaging
        public TestDeviceInfo Device20_OTAA { get; private set; }

        // Device21_ABP: Preferred 2nd window
        public TestDeviceInfo Device21_ABP { get; private set; }

        // Device22_ABP: used for ABP Mac Commands testing
        public TestDeviceInfo Device22_ABP { get; private set; }

        // Device23_OTAA: used for OTAA C2D Mac Commands testing
        public TestDeviceInfo Device23_OTAA { get; private set; }

        // Device24_ABP: Class C device
        public TestDeviceInfo Device24_ABP { get; private set; }

        // Arduino device used for testing
        public LoRaArduinoSerial ArduinoDevice
        {
            get { return this.arduinoDevice; }
        }

        private LoRaArduinoSerial arduinoDevice;

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            if (!string.IsNullOrEmpty(this.Configuration.LeafDeviceSerialPort))
            {
                this.arduinoDevice = LoRaArduinoSerial.CreateFromPort(this.Configuration.LeafDeviceSerialPort);
            }
            else
            {
                TestLogger.Log("[WARN] Not serial port defined for test");
            }
        }

        public override void ClearLogs()
        {
            this.ArduinoDevice?.ClearSerialLogs();
            base.ClearLogs();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                this.arduinoDevice?.Dispose();
                this.arduinoDevice = null;
            }
        }

        public override void SetupTestDevices()
        {
            var gatewayID = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID") ?? this.Configuration.LeafDeviceGatewayID;

            // Device1_OTAA: used for join test only
            this.Device1_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000001",
                AppEUI = "0000000000000001",
                AppKey = "00000000000000000000000000000001",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device2_OTAA: used for failed join (wrong devEUI)
            this.Device2_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000002",
                AppEUI = "0000000000000002",
                AppKey = "00000000000000000000000000000002",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = false,
            };

            // Device3_OTAA: used for failed join (wrong appKey)
            this.Device3_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000003",
                AppEUI = "0000000000000003",
                AppKey = "00000000000000000000000000000003",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
            };

            // Device4_OTAA: used for OTAA confirmed & unconfirmed messaging
            this.Device4_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000004",
                AppEUI = "0000000000000004",
                AppKey = "00000000000000000000000000000004",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                RX1DROffset = 1
            };

            // Device5_ABP: used for ABP confirmed & unconfirmed messaging
            this.Device5_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000005",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = "00000000000000000000000000000005",
                NwkSKey = "00000000000000000000000000000005",
                DevAddr = "0028B1B0",
            };

            // Device6_ABP: used for ABP wrong devaddr
            this.Device6_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000006",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = false,
                AppSKey = "00000000000000000000000000000006",
                NwkSKey = "00000000000000000000000000000006",
                DevAddr = "00000006",
            };

            // Device7_ABP: used for ABP wrong nwkskey
            this.Device7_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000007",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = "00000000000000000000000000000007",
                NwkSKey = "00000000000000000000000000000007",
                DevAddr = "00000007",
            };

            // Device8_ABP: used for ABP invalid nwkskey (mic fails)
            this.Device8_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000008",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = "00000000000000000000000000000008",
                NwkSKey = "00000000000000000000000000000008",
                DevAddr = "00000008",
            };

            // Device9_OTAA: used for confirmed message & C2D
            this.Device9_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000009",
                AppEUI = "0000000000000009",
                AppKey = "00000000000000000000000000000009",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                RXDelay = 2
            };

            // Device10_OTAA: used for unconfirmed message & C2D
            this.Device10_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000010",
                AppEUI = "0000000000000010",
                AppKey = "00000000000000000000000000000010",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device11_OTAA: used for http decoder
            this.Device11_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000011",
                AppEUI = "0000000000000011",
                AppKey = "00000000000000000000000000000011",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "http://sensordecodermodule/api/DecoderValueSensor",
            };

            // Device12_OTAA: used for reflection based decoder
            this.Device12_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000012",
                AppEUI = "0000000000000012",
                AppKey = "00000000000000000000000000000012",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            // Device13_OTAA: used for Join with wrong AppEUI
            this.Device13_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000013",
                AppEUI = "0000000000000013",
                AppKey = "00000000000000000000000000000013",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            // Device14_OTAA: used for Confirmed C2D message
            this.Device14_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000014",
                AppEUI = "0000000000000014",
                AppKey = "00000000000000000000000000000014",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            // Device15_OTAA: used for the Fport test
            this.Device15_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000015",
                AppEUI = "0000000000000015",
                AppKey = "00000000000000000000000000000015",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };
            // Device16_ABP: used for same DevAddr test
            this.Device16_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000016",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = "00000000000000000000000000000016",
                NwkSKey = "00000000000000000000000000000016",
                DevAddr = "00000016",
            };

            // Device17_ABP: used for same DevAddr test
            this.Device17_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000017",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = "00000000000000000000000000000017",
                NwkSKey = "00000000000000000000000000000017",
                DevAddr = this.Device16_ABP.DevAddr, // MUST match DevAddr from Device16
            };

            // Device18_ABP: used for C2D invalid fport testing
            this.Device18_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000018",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
                AppSKey = "00000000000000000000000000000018",
                NwkSKey = "00000000000000000000000000000018",
                DevAddr = "00000018",
            };

            // Device19_ABP: used for C2D invalid fport testing
            this.Device19_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000019",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
                AppSKey = "00000000000000000000000000000019",
                NwkSKey = "00000000000000000000000000000019",
                DevAddr = "00000019",
            };

            // Device20_OTAA: used for join and rejoin test
            this.Device20_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000020",
                AppEUI = "0000000000000020",
                AppKey = "00000000000000000000000000000020",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
                RX2DataRate = 3,
                PreferredWindow = 2
            };

            // Device21_ABP: Preferred 2nd window
            this.Device21_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000021",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
                AppSKey = "00000000000000000000000000000021",
                NwkSKey = "00000000000000000000000000000021",
                DevAddr = "00000021",
                PreferredWindow = 2
            };

            // Device22_ABP: used for mac Command testing
            this.Device22_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000022",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = "00000000000000000000000000000022",
                NwkSKey = "00000000000000000000000000000022",
                DevAddr = "00000022",
            };

            // Device23_OTAA: used for C2D mac Command testing
            this.Device23_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000023",
                AppEUI = "0000000000000023",
                AppKey = "00000000000000000000000000000023",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            // Device24_OTAA: used for C2D mac Command testing
            this.Device24_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000024",
                AppSKey = "00000000000000000000000000000024",
                NwkSKey = "00000000000000000000000000000024",
                DevAddr = "00000024",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
                ClassType = 'C',
            };
        }
    }
}
