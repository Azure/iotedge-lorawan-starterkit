// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Collections.Generic;
    using System.Globalization;

    public class TestDeviceInfo
    {
        // Device ID in IoT Hub
        public string DeviceID { get; set; }

        // Indicates if the device actually exists in IoT Hub
        public bool IsIoTHubDevice { get; set; }

        // Application Identifier
        // Used by OTAA devices
        public string AppEUI { get; set; }

        // Application Key
        // Dynamically activated devices (OTAA) use the Application Key (AppKey)
        // to derive the two session keys during the activation procedure
        public AppKey? AppKey { get; set; }

        // 32 bit device address (non-unique)
        // LoRaWAN devices have a 64 bit unique identifier that is assigned to the device
        // by the chip manufacturer, but communication uses 32 bit device address
        // In Over-the-Air Activation (OTAA) devices performs network join, where a DevAddr and security key are negotiated with the device.
        public string DevAddr { get; set; }

        // Application Session Key
        // Used for encryption and decryption of the payload
        public AppSessionKey? AppSKey { get; set; }

        // Network Session Key
        // Used for interaction between the device and Network Server.
        // This key is used to check the validity of messages (MIC check)
        public NetworkSessionKey? NwkSKey { get; set; }

        // Associated IoT Edge device
        public string GatewayID { get; set; }

        // Decoder used by the device
        // Project supports following values: DecoderGpsSensor, DecoderTemperatureSensor, DecoderValueSensor
        public string SensorDecoder { get; set; } = "DecoderValueSensor";

        public int PreferredWindow { get; set; } = 1;

        public char ClassType { get; set; } = 'A';

        public int RX2DataRate { get; set; }

        public uint RX1DROffset { get; set; }

        public bool Supports32BitFCnt { get; set; }

        public string Deduplication { get; set; }

        public ushort RXDelay { get; set; }

        public int KeepAliveTimeout { get; set; }

        public bool IsMultiGw => string.IsNullOrEmpty(GatewayID);

        /// <summary>
        /// Gets the desired properties for the <see cref="TestDeviceInfo"/>.
        /// </summary>
        public Dictionary<string, object> GetDesiredProperties()
        {
            var desiredProperties = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(AppEUI))
                desiredProperties[nameof(AppEUI)] = AppEUI;

            if (AppKey is { } someAppKey)
                desiredProperties[nameof(AppKey)] = someAppKey.ToString();

            if (!string.IsNullOrEmpty(GatewayID))
                desiredProperties[nameof(GatewayID)] = GatewayID;

            if (!string.IsNullOrEmpty(SensorDecoder))
                desiredProperties[nameof(SensorDecoder)] = SensorDecoder;

            if (AppSKey is { } someAppSessionKey)
                desiredProperties[nameof(AppSKey)] = someAppSessionKey.ToString();

            if (NwkSKey is { } someNetworkSessionKey)
                desiredProperties[nameof(NwkSKey)] = someNetworkSessionKey.ToString();

            if (!string.IsNullOrEmpty(DevAddr))
                desiredProperties[nameof(DevAddr)] = DevAddr;

            desiredProperties[nameof(PreferredWindow)] = PreferredWindow;

            if (char.ToLower(ClassType, CultureInfo.InvariantCulture) != 'a')
                desiredProperties[nameof(ClassType)] = ClassType.ToString();

            desiredProperties[nameof(RX1DROffset)] = RX1DROffset;

            desiredProperties[nameof(RX2DataRate)] = RX2DataRate;

            desiredProperties[nameof(RXDelay)] = RXDelay;

            // if (KeepAliveTimeout > 0)
            desiredProperties[nameof(KeepAliveTimeout)] = KeepAliveTimeout;

            if (!string.IsNullOrEmpty(Deduplication))
                desiredProperties[nameof(Deduplication)] = Deduplication;

            return desiredProperties;
        }

        /// <summary>
        /// Creates a <see cref="TestDeviceInfo"/> with ABP authentication.
        /// </summary>
        public static TestDeviceInfo CreateABPDevice(uint deviceID, string prefix = null, string gatewayID = null, string sensorDecoder = "DecoderValueSensor", uint netId = 1, char deviceClassType = 'A', bool supports32BitFcnt = false)
        {
            var value8 = deviceID.ToString("00000000", CultureInfo.InvariantCulture);
            var value16 = deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture);
            var value32 = deviceID.ToString("00000000000000000000000000000000", CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(prefix))
            {
                value8 = string.Concat(prefix, value8[prefix.Length..]);
                value16 = string.Concat(prefix, value16[prefix.Length..]);
                value32 = string.Concat(prefix, value32[prefix.Length..]);
            }

            var devAddrValue = NetIdHelper.SetNwkIdPart(value8, netId);
            var result = new TestDeviceInfo
            {
                DeviceID = value16,
                GatewayID = gatewayID,
                SensorDecoder = sensorDecoder,
                AppSKey = AppSessionKey.Parse(value32),
                NwkSKey = NetworkSessionKey.Parse(value32),
                DevAddr = devAddrValue,
                ClassType = deviceClassType,
                Supports32BitFCnt = supports32BitFcnt
            };

            return result;
        }

        /// <summary>
        /// Creates a <see cref="TestDeviceInfo"/> with OTAA authentication.
        /// </summary>
        /// <param name="deviceID">Device identifier. It will padded with 0's.</param>
        public static TestDeviceInfo CreateOTAADevice(uint deviceID, string prefix = null, string gatewayID = null, string sensorDecoder = "DecoderValueSensor", char deviceClassType = 'A')
        {
            var value16 = deviceID.ToString("0000000000000000", CultureInfo.InvariantCulture);
            var value32 = deviceID.ToString("00000000000000000000000000000000", CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(prefix))
            {
                value16 = string.Concat(prefix, value16[prefix.Length..]);
                value32 = string.Concat(prefix, value32[prefix.Length..]);
            }

            var result = new TestDeviceInfo
            {
                DeviceID = value16,
                AppEUI = value16,
                AppKey = LoRaWan.AppKey.Parse(value32),
                GatewayID = gatewayID,
                SensorDecoder = sensorDecoder,
                ClassType = deviceClassType,
            };

            return result;
        }
    }
}
