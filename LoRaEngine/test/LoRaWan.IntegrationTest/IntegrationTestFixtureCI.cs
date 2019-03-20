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

        // Device25_ABP: Connection timeout
        public TestDeviceInfo Device25_ABP { get; private set; }

        // Device26_ABP: Connection timeout
        public TestDeviceInfo Device26_ABP { get; private set; }

        /// <summary>
        /// Gets Device24_OTAA: used for OTAA deduplication testing
        /// </summary>
        public TestDeviceInfo Device25_OTAA { get; private set; }

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

        public string GetKey16(int deviceId)
        {
            var format = string.IsNullOrEmpty(this.Configuration.DeviceKeyFormat) ? "0000000000000000" : this.Configuration.DeviceKeyFormat;
            if (format.Length < 16)
            {
                format = format.PadLeft(16, '0');
            }

            return deviceId.ToString(format);
        }

        public string GetKey32(int deviceId)
        {
            var format = string.IsNullOrEmpty(this.Configuration.DeviceKeyFormat) ? "00000000000000000000000000000000" : this.Configuration.DeviceKeyFormat;
            if (format.Length < 32)
            {
                format = format.PadLeft(32, '0');
            }

            return deviceId.ToString(format);
        }

        public override void SetupTestDevices()
        {
            var gatewayID = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID") ?? this.Configuration.LeafDeviceGatewayID;

            // Device1_OTAA: used for join test only
            this.Device1_OTAA = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(1),
                AppEUI = this.GetKey16(1),
                AppKey = this.GetKey32(1),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device2_OTAA: used for failed join (wrong devEUI)
            this.Device2_OTAA = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(2),
                AppEUI = this.GetKey16(2),
                AppKey = this.GetKey32(2),
                GatewayID = gatewayID,
                IsIoTHubDevice = false,
            };

            // Device3_OTAA: used for failed join (wrong appKey)
            this.Device3_OTAA = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(3),
                AppEUI = this.GetKey16(3),
                AppKey = this.GetKey32(3),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device4_OTAA: used for OTAA confirmed & unconfirmed messaging
            this.Device4_OTAA = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(4),
                AppEUI = this.GetKey16(4),
                AppKey = this.GetKey32(4),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                RX1DROffset = 1
            };

            // Device5_ABP: used for ABP confirmed & unconfirmed messaging
            this.Device5_ABP = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(5),
                AppSKey = this.GetKey32(5),
                NwkSKey = this.GetKey32(5),
                DevAddr = "0028B1B0",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device6_ABP: used for ABP wrong devaddr
            this.Device6_ABP = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(6),
                AppSKey = this.GetKey32(6),
                NwkSKey = this.GetKey32(6),
                DevAddr = "00000006",
                GatewayID = gatewayID,
                IsIoTHubDevice = false,
            };

            // Device7_ABP: used for ABP wrong nwkskey
            this.Device7_ABP = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(7),
                AppSKey = this.GetKey32(7),
                NwkSKey = this.GetKey32(7),
                DevAddr = "00000007",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device8_ABP: used for ABP invalid nwkskey (mic fails)
            this.Device8_ABP = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(8),
                AppSKey = this.GetKey32(8),
                NwkSKey = this.GetKey32(8),
                DevAddr = "00000008",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device9_OTAA: used for confirmed message & C2D
            this.Device9_OTAA = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(9),
                AppEUI = this.GetKey16(9),
                AppKey = this.GetKey32(9),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                RXDelay = 2
            };

            // Device10_OTAA: used for unconfirmed message & C2D
            this.Device10_OTAA = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(10),
                AppEUI = this.GetKey16(10),
                AppKey = this.GetKey32(10),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device11_OTAA: used for http decoder
            this.Device11_OTAA = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(11),
                AppEUI = this.GetKey16(11),
                AppKey = this.GetKey32(11),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "http://sensordecodermodule/api/DecoderValueSensor",
            };

            // Device12_OTAA: used for reflection based decoder
            this.Device12_OTAA = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(12),
                AppEUI = this.GetKey16(12),
                AppKey = this.GetKey32(12),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device13_OTAA: used for Join with wrong AppEUI
            this.Device13_OTAA = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(13),
                AppEUI = this.GetKey16(13),
                AppKey = this.GetKey32(13),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device14_OTAA: used for Confirmed C2D message
            this.Device14_OTAA = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(14),
                AppEUI = this.GetKey16(14),
                AppKey = this.GetKey32(14),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device15_OTAA: used for the Fport test
            this.Device15_OTAA = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(15),
                AppEUI = this.GetKey16(15),
                AppKey = this.GetKey32(15),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };
            // Device16_ABP: used for same DevAddr test
            this.Device16_ABP = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(16),
                AppSKey = this.GetKey32(16),
                NwkSKey = this.GetKey32(16),
                DevAddr = "00000016",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device17_ABP: used for same DevAddr test
            this.Device17_ABP = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(17),
                AppSKey = this.GetKey32(17),
                NwkSKey = this.GetKey32(17),
                DevAddr = this.Device16_ABP.DevAddr, // MUST match DevAddr from Device16
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device18_ABP: used for C2D invalid fport testing
            this.Device18_ABP = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(18),
                AppSKey = this.GetKey32(18),
                NwkSKey = this.GetKey32(18),
                DevAddr = "00000018",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            // Device19_ABP: used for C2D invalid fport testing
            this.Device19_ABP = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(19),
                AppSKey = this.GetKey32(19),
                NwkSKey = this.GetKey32(19),
                DevAddr = "00000019",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            // Device20_OTAA: used for join and rejoin test
            this.Device20_OTAA = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(20),
                AppEUI = this.GetKey16(20),
                AppKey = this.GetKey32(20),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                RX2DataRate = 3,
                PreferredWindow = 2
            };

            // Device21_ABP: Preferred 2nd window
            this.Device21_ABP = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(21),
                AppSKey = this.GetKey32(21),
                NwkSKey = this.GetKey32(21),
                DevAddr = "00000021",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
                PreferredWindow = 2
            };

            // Device22_ABP: used for mac Command testing
            this.Device22_ABP = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(22),
                AppSKey = this.GetKey32(22),
                NwkSKey = this.GetKey32(22),
                DevAddr = "00000022",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device23_OTAA: used for C2D mac Command testing
            this.Device23_OTAA = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(23),
                AppEUI = this.GetKey16(23),
                AppKey = this.GetKey32(23),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device24_OTAA: used for C2D mac Command testing
            this.Device24_ABP = new TestDeviceInfo()
            {
                DeviceID = this.GetKey16(24),
                AppSKey = this.GetKey32(24),
                NwkSKey = this.GetKey32(24),
                DevAddr = "00000024",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                ClassType = 'C',
            };

            // Device25_ABP: Connection timeout
            this.Device25_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000025",
                AppSKey = "00000000000000000000000000000025",
                NwkSKey = "00000000000000000000000000000025",
                DevAddr = "00000025",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
                KeepAliveTimeout = 60,
            };

            // Device26_ABP: Connection timeout
            this.Device26_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000026",
                AppSKey = "00000000000000000000000000000026",
                NwkSKey = "00000000000000000000000000000026",
                DevAddr = "00000026",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            this.Device25_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000025",
                AppEUI = "0000000000000025",
                AppKey = "00000000000000000000000000000025",
                IsIoTHubDevice = true,
                Deduplication = "Drop"
            };
        }
    }
}
