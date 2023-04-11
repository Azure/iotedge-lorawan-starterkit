// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Collections.Generic;
    using System.Globalization;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using static ReceiveWindowNumber;

    public class TestDeviceInfo
    {
        // Device ID in IoT Hub
        public string DeviceID { get; set; }

        public DevEui DevEui => DevEui.Parse(DeviceID);

        // Indicates if the device actually exists in IoT Hub
        public bool IsIoTHubDevice { get; set; }

        // Application Identifier
        // Used by OTAA devices
        public JoinEui? AppEui { get; set; }

        // Application Key
        // Dynamically activated devices (OTAA) use the Application Key (AppKey)
        // to derive the two session keys during the activation procedure
        public AppKey? AppKey { get; set; }

        // 32 bit device address (non-unique)
        // LoRaWAN devices have a 64 bit unique identifier that is assigned to the device
        // by the chip manufacturer, but communication uses 32 bit device address
        // In Over-the-Air Activation (OTAA) devices performs network join, where a DevAddr and security key are negotiated with the device.
        public DevAddr? DevAddr { get; set; }

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

        public ReceiveWindowNumber PreferredWindow { get; set; } = ReceiveWindow1;

        public LoRaDeviceClassType ClassType { get; set; } = LoRaDeviceClassType.A;

        public int RX2DataRate { get; set; }

        public uint RX1DROffset { get; set; }

        public bool Supports32BitFCnt { get; set; }

        public DeduplicationMode Deduplication { get; set; } = DeduplicationMode.Drop; // default to drop

        public ushort RXDelay { get; set; }

        public int KeepAliveTimeout { get; set; }

        public bool IsMultiGw => string.IsNullOrEmpty(GatewayID);

        public object RouterConfig { get; set; }

        /// <summary>
        /// Gets the desired properties for the <see cref="TestDeviceInfo"/>.
        /// </summary>
        public Dictionary<string, object> GetDesiredProperties()
        {
            var desiredProperties = new Dictionary<string, object>();
            if (AppEui is { } someAppEui)
                desiredProperties[TwinProperty.AppEui] = someAppEui.ToString();

            if (AppKey is { } someAppKey)
                desiredProperties[TwinProperty.AppKey] = someAppKey.ToString();

            if (!string.IsNullOrEmpty(GatewayID))
                desiredProperties[TwinProperty.GatewayID] = GatewayID;

            if (!string.IsNullOrEmpty(SensorDecoder))
                desiredProperties[TwinProperty.SensorDecoder] = SensorDecoder;

            if (AppSKey is { } someAppSessionKey)
                desiredProperties[TwinProperty.AppSKey] = someAppSessionKey.ToString();

            if (NwkSKey is { } someNetworkSessionKey)
                desiredProperties[TwinProperty.NwkSKey] = someNetworkSessionKey.ToString();

            if (DevAddr is { } someDevAddr)
                desiredProperties[TwinProperty.DevAddr] = someDevAddr.ToString();

            if (RouterConfig is { } routerConfig)
                desiredProperties[BasicsStationConfigurationService.RouterConfigPropertyName] = routerConfig;

            desiredProperties[TwinProperty.PreferredWindow] = (int)PreferredWindow;

            if (ClassType != LoRaDeviceClassType.A)
                desiredProperties[TwinProperty.ClassType] = ClassType.ToString();

            desiredProperties[TwinProperty.RX1DROffset] = RX1DROffset;

            desiredProperties[TwinProperty.RX2DataRate] = RX2DataRate;

            desiredProperties[TwinProperty.RXDelay] = RXDelay;

            // if (KeepAliveTimeout > 0)
            desiredProperties[TwinProperty.KeepAliveTimeout] = KeepAliveTimeout;

            if (Deduplication is not DeduplicationMode.Drop)
                desiredProperties[TwinProperty.Deduplication] = Deduplication.ToString();

            return desiredProperties;
        }

        /// <summary>
        /// Creates a <see cref="TestDeviceInfo"/> with ABP authentication.
        /// </summary>
        public static TestDeviceInfo CreateABPDevice(uint deviceID,
                                                     string prefix = null,
                                                     string gatewayID = null,
                                                     string sensorDecoder = "DecoderValueSensor",
                                                     int netId = 1,
                                                     LoRaDeviceClassType deviceClassType = LoRaDeviceClassType.A,
                                                     bool supports32BitFcnt = false)
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

            var result = new TestDeviceInfo
            {
                DeviceID = value16,
                GatewayID = gatewayID,
                SensorDecoder = sensorDecoder,
                AppSKey = AppSessionKey.Parse(value32),
                NwkSKey = NetworkSessionKey.Parse(value32),
                DevAddr = LoRaWan.DevAddr.Parse(value8) with { NetworkId = unchecked((byte)netId & 0x7f) },
                ClassType = deviceClassType,
                Supports32BitFCnt = supports32BitFcnt
            };

            return result;
        }

        /// <summary>
        /// Creates a <see cref="TestDeviceInfo"/> with OTAA authentication.
        /// </summary>
        /// <param name="deviceID">Device identifier. It will padded with 0's.</param>
        public static TestDeviceInfo CreateOTAADevice(uint deviceID,
                                                      string prefix = null,
                                                      string gatewayID = null,
                                                      string sensorDecoder = "DecoderValueSensor",
                                                      LoRaDeviceClassType deviceClassType = LoRaDeviceClassType.A)
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
                AppEui = JoinEui.Parse(value16),
                AppKey = LoRaWan.AppKey.Parse(value32),
                GatewayID = gatewayID,
                SensorDecoder = sensorDecoder,
                ClassType = deviceClassType,
            };

            return result;
        }
    }
}
