// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using LoRaWan.Tests.Common;

    public class IntegrationTestFixtureCi : IntegrationTestFixtureBase
    {
        // Device1_OTAA: used for join test only
        public TestDeviceInfo Device1_OTAA { get; private set; }

        // Device2_OTAA: used for failed join (wrong devEUI)
        public TestDeviceInfo Device2_OTAA { get; private set; }

        // Device3_OTAA: used for failed join (wrong appKey)
        public TestDeviceInfo Device3_OTAA { get; private set; }

        // Device4_OTAA: used for OTAA confirmed & unconfirmed messaging
        public TestDeviceInfo Device4_OTAA { get; private set; }

        public TestDeviceInfo Device4_OTAA_MultiGw { get; private set; }

        // Device5_ABP: used for ABP confirmed & unconfirmed messaging
        public TestDeviceInfo Device5_ABP { get; private set; }

        public TestDeviceInfo Device5_ABP_MultiGw { get; private set; }

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

        public TestDeviceInfo Device14_OTAA_MultiGw { get; private set; }

        // Device15_OTAA: used for test fport C2D
        public TestDeviceInfo Device15_OTAA { get; private set; }

        public TestDeviceInfo Device15_OTAA_MultiGw { get; private set; }

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

        public TestDeviceInfo Device20_OTAA_MultiGw { get; private set; }

        // Device21_ABP: Preferred 2nd window
        public TestDeviceInfo Device21_ABP { get; private set; }

        // Device22_ABP: used for ABP Mac Commands testing
        public TestDeviceInfo Device22_ABP { get; private set; }

        // Device23_OTAA: used for OTAA C2D Mac Commands testing
        public TestDeviceInfo Device23_OTAA { get; private set; }

        public TestDeviceInfo Device23_OTAA_MultiGw { get; private set; }

        // Device24_ABP: Class C device
        public TestDeviceInfo Device24_ABP { get; private set; }

        // Device25_ABP: Connection timeout
        public TestDeviceInfo Device25_ABP { get; private set; }

        // Device26_ABP: Connection timeout
        public TestDeviceInfo Device26_ABP { get; private set; }

        /// <summary>
        /// Gets Device27_OTAA: used for multi GW OTAA testing.
        /// </summary>
        public TestDeviceInfo Device27_OTAA { get; private set; }

        /// <summary>
        /// Gets Device27_ABP: used for multi GW deduplication drop testing.
        /// </summary>
        public TestDeviceInfo Device28_ABP { get; private set; }

        /// <summary>
        /// Gets Device27_ABP: used for multi GW deduplication mark testing.
        /// </summary>
        public TestDeviceInfo Device29_ABP { get; private set; }

        /// <summary>
        /// Gets Device30_OTAA used for C2D messages in multi gateway scenario.
        /// </summary>
        public TestDeviceInfo Device30_OTAA { get; private set; }

        // Arduino device used for testing
        public LoRaArduinoSerial ArduinoDevice { get; private set; }

        public override async Task InitializeAsync()
        {
            TestLogger.LogDate = true;

            await base.InitializeAsync();

            if (!string.IsNullOrEmpty(Configuration.LeafDeviceSerialPort))
            {
                ArduinoDevice = LoRaArduinoSerial.CreateFromPort(Configuration.LeafDeviceSerialPort);
            }
            else
            {
                TestLogger.Log("[WARN] Not serial port defined for test");
            }

            LoRaAPIHelper.Initialize(Configuration.FunctionAppCode, Configuration.FunctionAppBaseUrl);
        }

        public override void ClearLogs()
        {
            ArduinoDevice?.ClearSerialLogs();
            base.ClearLogs();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                ArduinoDevice?.Dispose();
                ArduinoDevice = null;
            }
        }

        public string GetKey16(int deviceId, bool multiGw = false)
        {
            var target = multiGw ? Configuration.DeviceKeyFormatMultiGW : Configuration.DeviceKeyFormat;
            var format = string.IsNullOrEmpty(target) ? "0000000000000000" : target;

            if (format.Length < 16)
            {
                format = format.PadLeft(16, '0');
            }

            return deviceId.ToString(format, CultureInfo.InvariantCulture);
        }

        public string GetKey32(int deviceId, bool multiGw = false)
        {
            var target = multiGw ? Configuration.DeviceKeyFormatMultiGW : Configuration.DeviceKeyFormat;
            var format = string.IsNullOrEmpty(target) ? "00000000000000000000000000000000" : target;
            if (format.Length < 32)
            {
                format = format.PadLeft(32, '0');
            }

            return deviceId.ToString(format, CultureInfo.InvariantCulture);
        }

        public override void SetupTestDevices()
        {
            var gatewayID = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID") ?? Configuration.LeafDeviceGatewayID;

            // Device1_OTAA: used for join test only
            Device1_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(1),
                AppEUI = GetKey16(1),
                AppKey = GetKey32(1),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device2_OTAA: used for failed join (wrong devEUI)
            Device2_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(2),
                AppEUI = GetKey16(2),
                AppKey = GetKey32(2),
                GatewayID = gatewayID,
                IsIoTHubDevice = false,
            };

            // Device3_OTAA: used for failed join (wrong appKey)
            Device3_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(3),
                AppEUI = GetKey16(3),
                AppKey = GetKey32(3),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device4_OTAA: used for OTAA confirmed & unconfirmed messaging
            Device4_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(4),
                AppEUI = GetKey16(4),
                AppKey = GetKey32(4),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                RX1DROffset = 1
            };

            Device4_OTAA_MultiGw = new TestDeviceInfo()
            {
                DeviceID = GetKey16(4, true),
                AppEUI = GetKey16(4, true),
                AppKey = GetKey32(4, true),
                IsIoTHubDevice = true,
                RX1DROffset = 1
            };

            // Device5_ABP: used for ABP confirmed & unconfirmed messaging
            Device5_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(5),
                AppSKey = GetKey32(5),
                NwkSKey = GetKey32(5),
                DevAddr = "0028B1B0",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            Device5_ABP_MultiGw = new TestDeviceInfo()
            {
                DeviceID = GetKey16(5, true),
                AppSKey = GetKey32(5, true),
                NwkSKey = GetKey32(5, true),
                DevAddr = "0028B1B0",
                IsIoTHubDevice = true,
            };

            // Device6_ABP: used for ABP wrong devaddr
            Device6_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(6),
                AppSKey = GetKey32(6),
                NwkSKey = GetKey32(6),
                DevAddr = "00000006",
                GatewayID = gatewayID,
                IsIoTHubDevice = false,
            };

            // Device7_ABP: used for ABP wrong nwkskey
            Device7_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(7),
                AppSKey = GetKey32(7),
                NwkSKey = GetKey32(7),
                DevAddr = "00000007",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device8_ABP: used for ABP invalid nwkskey (mic fails)
            Device8_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(8),
                AppSKey = GetKey32(8),
                NwkSKey = GetKey32(8),
                DevAddr = "00000008",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device9_OTAA: used for confirmed message & C2D
            Device9_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(9),
                AppEUI = GetKey16(9),
                AppKey = GetKey32(9),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                RXDelay = 2
            };

            // Device10_OTAA: used for unconfirmed message & C2D
            Device10_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(10),
                AppEUI = GetKey16(10),
                AppKey = GetKey32(10),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device11_OTAA: used for http decoder
            Device11_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(11),
                AppEUI = GetKey16(11),
                AppKey = GetKey32(11),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "http://sensordecodermodule/api/DecoderValueSensor",
            };

            // Device12_OTAA: used for reflection based decoder
            Device12_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(12),
                AppEUI = GetKey16(12),
                AppKey = GetKey32(12),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device13_OTAA: used for Join with wrong AppEUI
            Device13_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(13),
                AppEUI = GetKey16(13),
                AppKey = GetKey32(13),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device14_OTAA: used for Confirmed C2D message
            Device14_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(14),
                AppEUI = GetKey16(14),
                AppKey = GetKey32(14),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            Device14_OTAA_MultiGw = new TestDeviceInfo()
            {
                DeviceID = GetKey16(14, true),
                AppEUI = GetKey16(14, true),
                AppKey = GetKey32(14, true),
                IsIoTHubDevice = true,
            };

            // Device15_OTAA: used for the Fport test
            Device15_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(15),
                AppEUI = GetKey16(15),
                AppKey = GetKey32(15),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            Device15_OTAA_MultiGw = new TestDeviceInfo()
            {
                DeviceID = GetKey16(15, true),
                AppEUI = GetKey16(15, true),
                AppKey = GetKey32(15, true),
                IsIoTHubDevice = true,
            };
            // Device16_ABP: used for same DevAddr test
            Device16_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(16),
                AppSKey = GetKey32(16),
                NwkSKey = GetKey32(16),
                DevAddr = "00000016",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device17_ABP: used for same DevAddr test
            Device17_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(17),
                AppSKey = GetKey32(17),
                NwkSKey = GetKey32(17),
                DevAddr = Device16_ABP.DevAddr, // MUST match DevAddr from Device16
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device18_ABP: used for C2D invalid fport testing
            Device18_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(18),
                AppSKey = GetKey32(18),
                NwkSKey = GetKey32(18),
                DevAddr = "00000018",
                GatewayID = gatewayID,
                IsIoTHubDevice = true
            };

            // Device19_ABP: used for C2D invalid fport testing
            Device19_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(19),
                AppSKey = GetKey32(19),
                NwkSKey = GetKey32(19),
                DevAddr = "00000019",
                GatewayID = gatewayID,
                IsIoTHubDevice = true
            };

            // Device20_OTAA: used for join and rejoin test
            Device20_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(20),
                AppEUI = GetKey16(20),
                AppKey = GetKey32(20),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                RX2DataRate = 3,
                PreferredWindow = 2
            };

            Device20_OTAA_MultiGw = new TestDeviceInfo()
            {
                DeviceID = GetKey16(20, true),
                AppEUI = GetKey16(20, true),
                AppKey = GetKey32(20, true),
                IsIoTHubDevice = true,
                RX2DataRate = 3,
                PreferredWindow = 2
            };

            // Device21_ABP: Preferred 2nd window
            Device21_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(21),
                AppSKey = GetKey32(21),
                NwkSKey = GetKey32(21),
                DevAddr = "00000021",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                PreferredWindow = 2
            };

            // Device22_ABP: used for mac Command testing
            Device22_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(22),
                AppSKey = GetKey32(22),
                NwkSKey = GetKey32(22),
                DevAddr = "00000022",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device23_OTAA: used for C2D mac Command testing
            Device23_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(23),
                AppEUI = GetKey16(23),
                AppKey = GetKey32(23),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            Device23_OTAA_MultiGw = new TestDeviceInfo()
            {
                DeviceID = GetKey16(23, true),
                AppEUI = GetKey16(23, true),
                AppKey = GetKey32(23, true),
                IsIoTHubDevice = true
            };

            // Device24_OTAA: used for C2D mac Command testing
            Device24_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(24),
                AppSKey = GetKey32(24),
                NwkSKey = GetKey32(24),
                DevAddr = "00000024",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                ClassType = 'C',
            };

            // Device25_ABP: Connection timeout
            Device25_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000025",
                AppSKey = "00000000000000000000000000000025",
                NwkSKey = "00000000000000000000000000000025",
                DevAddr = "00000025",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                KeepAliveTimeout = 60
            };

            // Device26_ABP: Connection timeout
            Device26_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000026",
                AppSKey = "00000000000000000000000000000026",
                NwkSKey = "00000000000000000000000000000026",
                DevAddr = "00000026",
                GatewayID = gatewayID,
                IsIoTHubDevice = true
            };

            Device27_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000027",
                AppEUI = "0000000000000027",
                AppKey = "00000000000000000000000000000027",
                IsIoTHubDevice = true
            };

            Device28_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000028",
                AppSKey = "00000000000000000000000000000028",
                NwkSKey = "00000000000000000000000000000028",
                DevAddr = "00000027",
                IsIoTHubDevice = true,
                Deduplication = "Drop"
            };

            Device29_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000029",
                AppSKey = "00000000000000000000000000000029",
                NwkSKey = "00000000000000000000000000000029",
                DevAddr = "00000029",
                IsIoTHubDevice = true,
                Deduplication = "Mark"
            };

            Device30_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(30),
                AppEUI = GetKey16(30),
                AppKey = GetKey32(30),
                IsIoTHubDevice = true
            };
        }
    }
}
