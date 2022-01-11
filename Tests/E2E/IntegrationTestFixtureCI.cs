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

        // Device6_ABP: Not Used at the moment. Was used for wrong devaddr
        // But test dropped as part of LBS migration.
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

        /// <summary>
        /// Gets Device31_OTAA: used for concentrator deduplication testing in a single gateway scenario.
        /// </summary>
        public TestDeviceInfo Device31_OTAA { get; private set; }

        /// <summary>
        /// Gets Device32_ABP: used for testing C2D message with LinkADRRequest.
        /// </summary>
        public TestDeviceInfo Device32_ABP { get; private set; }

        /// <summary>
        /// Gets Device33_OTAA: used for testing successful message sent after CUPS.
        /// </summary>
        public TestDeviceInfo Device33_OTAA { get; private set; }

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

        public AppSessionKey GetAppSessionKey(int deviceId, bool multiGw = false) =>
            AppSessionKey.Parse(GetKey32(deviceId, multiGw));

        public AppKey GetAppKey(int deviceId, bool multiGw = false) =>
           AppKey.Parse(GetKey32(deviceId, multiGw));

        public JoinEui GetJoinEui(int deviceId, bool multiGw = false) =>
           JoinEui.Parse(GetKey16(deviceId, multiGw));

        public NetworkSessionKey GetNetworkSessionKey(int deviceId, bool multiGw = false) =>
           NetworkSessionKey.Parse(GetKey32(deviceId, multiGw));


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
                AppEui = GetJoinEui(1),
                AppKey = GetAppKey(1),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device2_OTAA: used for failed join (wrong devEUI)
            Device2_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(2),
                AppEui = GetJoinEui(2),
                AppKey = GetAppKey(2),
                GatewayID = gatewayID,
                IsIoTHubDevice = false,
            };

            // Device3_OTAA: used for failed join (wrong appKey)
            Device3_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(3),
                AppEui = GetJoinEui(3),
                AppKey = GetAppKey(3),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device4_OTAA: used for OTAA confirmed & unconfirmed messaging
            Device4_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(4),
                AppEui = GetJoinEui(4),
                AppKey = GetAppKey(4),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                RX1DROffset = 1
            };

            Device4_OTAA_MultiGw = new TestDeviceInfo()
            {
                DeviceID = GetKey16(4, true),
                AppEui = GetJoinEui(4, true),
                AppKey = GetAppKey(4, true),
                IsIoTHubDevice = true,
                RX1DROffset = 1
            };

            // Device5_ABP: used for ABP confirmed & unconfirmed messaging
            Device5_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(5),
                AppSKey = GetAppSessionKey(5),
                NwkSKey = GetNetworkSessionKey(5),
                DevAddr = new DevAddr(0x0028b1b0),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            Device5_ABP_MultiGw = new TestDeviceInfo()
            {
                DeviceID = GetKey16(5, true),
                AppSKey = GetAppSessionKey(5, true),
                NwkSKey = GetNetworkSessionKey(5, true),
                DevAddr = new DevAddr(0x0028b1b0),
                IsIoTHubDevice = true,
            };

            // Device6_ABP: used for ABP wrong devaddr
            Device6_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(6),
                AppSKey = GetAppSessionKey(6),
                NwkSKey = GetNetworkSessionKey(6),
                DevAddr = new DevAddr(0x00000006),
                GatewayID = gatewayID,
                IsIoTHubDevice = false,
            };

            // Device7_ABP: used for ABP wrong nwkskey
            Device7_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(7),
                AppSKey = GetAppSessionKey(7),
                NwkSKey = GetNetworkSessionKey(7),
                DevAddr = new DevAddr(0x00000007),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device8_ABP: used for ABP invalid nwkskey (mic fails)
            Device8_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(8),
                AppSKey = GetAppSessionKey(8),
                NwkSKey = GetNetworkSessionKey(8),
                DevAddr = new DevAddr(0x00000008),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device9_OTAA: used for confirmed message & C2D
            Device9_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(9),
                AppEui = GetJoinEui(9),
                AppKey = GetAppKey(9),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                RXDelay = 2
            };

            // Device10_OTAA: used for unconfirmed message & C2D
            Device10_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(10),
                AppEui = GetJoinEui(10),
                AppKey = GetAppKey(10),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device11_OTAA: used for http decoder
            Device11_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(11),
                AppEui = GetJoinEui(11),
                AppKey = GetAppKey(11),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "http://sensordecodermodule/api/DecoderValueSensor",
            };

            // Device12_OTAA: used for reflection based decoder
            Device12_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(12),
                AppEui = GetJoinEui(12),
                AppKey = GetAppKey(12),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device13_OTAA: used for Join with wrong AppEUI
            Device13_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(13),
                AppEui = GetJoinEui(13),
                AppKey = GetAppKey(13),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device14_OTAA: used for Confirmed C2D message
            Device14_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(14),
                AppEui = GetJoinEui(14),
                AppKey = GetAppKey(14),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            Device14_OTAA_MultiGw = new TestDeviceInfo()
            {
                DeviceID = GetKey16(14, true),
                AppEui = GetJoinEui(14, true),
                AppKey = GetAppKey(14, true),
                IsIoTHubDevice = true,
            };

            // Device15_OTAA: used for the Fport test
            Device15_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(15),
                AppEui = GetJoinEui(15),
                AppKey = GetAppKey(15),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            Device15_OTAA_MultiGw = new TestDeviceInfo()
            {
                DeviceID = GetKey16(15, true),
                AppEui = GetJoinEui(15, true),
                AppKey = GetAppKey(15, true),
                IsIoTHubDevice = true,
            };
            // Device16_ABP: used for same DevAddr test
            Device16_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(16),
                AppSKey = GetAppSessionKey(16),
                NwkSKey = GetNetworkSessionKey(16),
                DevAddr = new DevAddr(0x00000016),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device17_ABP: used for same DevAddr test
            Device17_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(17),
                AppSKey = GetAppSessionKey(17),
                NwkSKey = GetNetworkSessionKey(17),
                DevAddr = Device16_ABP.DevAddr, // MUST match DevAddr from Device16
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device18_ABP: used for C2D invalid fport testing
            Device18_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(18),
                AppSKey = GetAppSessionKey(18),
                NwkSKey = GetNetworkSessionKey(18),
                DevAddr = new DevAddr(0x00000018),
                GatewayID = gatewayID,
                IsIoTHubDevice = true
            };

            // Device19_ABP: used for C2D invalid fport testing
            Device19_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(19),
                AppSKey = GetAppSessionKey(19),
                NwkSKey = GetNetworkSessionKey(19),
                DevAddr = new DevAddr(0x00000019),
                GatewayID = gatewayID,
                IsIoTHubDevice = true
            };

            // Device20_OTAA: used for join and rejoin test
            Device20_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(20),
                AppEui = GetJoinEui(20),
                AppKey = GetAppKey(20),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                RX2DataRate = 3,
                PreferredWindow = 2
            };

            Device20_OTAA_MultiGw = new TestDeviceInfo()
            {
                DeviceID = GetKey16(20, true),
                AppEui = GetJoinEui(20, true),
                AppKey = GetAppKey(20, true),
                IsIoTHubDevice = true,
                RX2DataRate = 3,
                PreferredWindow = 2
            };

            // Device21_ABP: Preferred 2nd window
            Device21_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(21),
                AppSKey = GetAppSessionKey(21),
                NwkSKey = GetNetworkSessionKey(21),
                DevAddr = new DevAddr(0x00000021),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                PreferredWindow = 2
            };

            // Device22_ABP: used for mac Command testing
            Device22_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(22),
                AppSKey = GetAppSessionKey(22),
                NwkSKey = GetNetworkSessionKey(22),
                DevAddr = new DevAddr(0x00000022),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device23_OTAA: used for C2D mac Command testing
            Device23_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(23),
                AppEui = GetJoinEui(23),
                AppKey = GetAppKey(23),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            Device23_OTAA_MultiGw = new TestDeviceInfo()
            {
                DeviceID = GetKey16(23, true),
                AppEui = GetJoinEui(23, true),
                AppKey = GetAppKey(23, true),
                IsIoTHubDevice = true
            };

            // Device24_OTAA: used for C2D mac Command testing
            Device24_ABP = new TestDeviceInfo()
            {
                DeviceID = GetKey16(24),
                AppSKey = GetAppSessionKey(24),
                NwkSKey = GetNetworkSessionKey(24),
                DevAddr = new DevAddr(0x00000024),
                IsIoTHubDevice = true,
                ClassType = 'C',
            };

            // Device25_ABP: Connection timeout
            Device25_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000025",
                AppSKey = GetAppSessionKey(25),
                NwkSKey = GetNetworkSessionKey(25),
                DevAddr = new DevAddr(0x00000025),
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                KeepAliveTimeout = 60
            };

            // Device26_ABP: Connection timeout
            Device26_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000026",
                AppSKey = GetAppSessionKey(26),
                NwkSKey = GetNetworkSessionKey(26),
                DevAddr = new DevAddr(0x00000026),
                GatewayID = gatewayID,
                IsIoTHubDevice = true
            };

            Device27_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000027",
                AppEui = GetJoinEui(27),
                AppKey = GetAppKey(27),
                IsIoTHubDevice = true
            };

            Device28_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000028",
                AppSKey = GetAppSessionKey(28),
                NwkSKey = GetNetworkSessionKey(28),
                DevAddr = new DevAddr(0x00000027),
                IsIoTHubDevice = true,
                Deduplication = "Drop"
            };

            Device29_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000029",
                AppSKey = GetAppSessionKey(29),
                NwkSKey = GetNetworkSessionKey(29),
                DevAddr = new DevAddr(0x00000029),
                IsIoTHubDevice = true,
                Deduplication = "Mark"
            };

            Device30_OTAA = new TestDeviceInfo()
            {
                DeviceID = GetKey16(30),
                AppEui = GetJoinEui(30),
                AppKey = GetAppKey(30),
                IsIoTHubDevice = true
            };

            Device31_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000031",
                AppEui = GetJoinEui(31),
                AppKey = GetAppKey(31),
                IsIoTHubDevice = true,
                Deduplication = "Drop"
            };

            Device32_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000032",
                AppSKey = GetAppSessionKey(32),
                NwkSKey = GetNetworkSessionKey(32),
                DevAddr = new DevAddr(0x00000032),
                GatewayID = gatewayID,
                IsIoTHubDevice = true
            };

            Device33_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000033",
                AppEui = GetJoinEui(33),
                AppKey = GetAppKey(33),
                IsIoTHubDevice = true
            };
        }
    }
}
