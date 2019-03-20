// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Test.Shared
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.Utils;

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
        public string AppKey { get; set; }

        // 32 bit device address (non-unique)
        // LoRaWAN devices have a 64 bit unique identifier that is assigned to the device
        // by the chip manufacturer, but communication uses 32 bit device address
        // In Over-the-Air Activation (OTAA) devices performs network join, where a DevAddr and security key are negotiated with the device.
        public string DevAddr { get; set; }

        // Application Session Key
        // Used for encryption and decryption of the payload
        public string AppSKey { get; set; }

        // Network Session Key
        // Used for interaction between the device and Network Server.
        // This key is used to check the validity of messages (MIC check)
        public string NwkSKey { get; set; }

        // Associated IoT Edge device
        public string GatewayID { get; set; }

        // Decoder used by the device
        // Project supports following values: DecoderGpsSensor, DecoderTemperatureSensor, DecoderValueSensor
        public string SensorDecoder { get; set; } = "DecoderValueSensor";

        public int PreferredWindow { get; set; } = 1;

        public char ClassType { get; set; } = 'A';

        public int RX2DataRate { get; set; } = 0;

        public uint RX1DROffset { get; set; } = 0;

        public bool Supports32BitFCnt { get; set; }

        public string Deduplication { get; set; }

        public ushort RXDelay { get; set; } = 0;

        public int KeepAliveTimeout { get; set; }

        /// <summary>
        /// Gets the desired properties for the <see cref="TestDeviceInfo"/>
        /// </summary>
        public Dictionary<string, object> GetDesiredProperties()
        {
            var desiredProperties = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(this.AppEUI))
                desiredProperties[nameof(this.AppEUI)] = this.AppEUI;

            if (!string.IsNullOrEmpty(this.AppKey))
                desiredProperties[nameof(this.AppKey)] = this.AppKey;

            if (!string.IsNullOrEmpty(this.GatewayID))
                desiredProperties[nameof(this.GatewayID)] = this.GatewayID;

            if (!string.IsNullOrEmpty(this.SensorDecoder))
                desiredProperties[nameof(this.SensorDecoder)] = this.SensorDecoder;

            if (!string.IsNullOrEmpty(this.AppSKey))
                desiredProperties[nameof(this.AppSKey)] = this.AppSKey;

            if (!string.IsNullOrEmpty(this.NwkSKey))
                desiredProperties[nameof(this.NwkSKey)] = this.NwkSKey;

            if (!string.IsNullOrEmpty(this.DevAddr))
                desiredProperties[nameof(this.DevAddr)] = this.DevAddr;

            desiredProperties[nameof(this.PreferredWindow)] = this.PreferredWindow;

            if (char.ToLower(this.ClassType) != 'a')
                desiredProperties[nameof(this.ClassType)] = this.ClassType.ToString();

            desiredProperties[nameof(this.RX1DROffset)] = this.RX1DROffset;

            desiredProperties[nameof(this.RX2DataRate)] = this.RX2DataRate;

            desiredProperties[nameof(this.RXDelay)] = this.RXDelay;

            // if (this.KeepAliveTimeout > 0)
            desiredProperties[nameof(this.KeepAliveTimeout)] = this.KeepAliveTimeout;

            if (!string.IsNullOrEmpty(this.Deduplication))
                desiredProperties[nameof(this.Deduplication)] = this.Deduplication;

            return desiredProperties;
        }

        /// <summary>
        /// Creates a <see cref="TestDeviceInfo"/> with ABP authentication
        /// </summary>
        public static TestDeviceInfo CreateABPDevice(uint deviceID, string prefix = null, string gatewayID = null, string sensorDecoder = "DecoderValueSensor", uint netId = 1, char deviceClassType = 'A', bool supports32BitFcnt = false)
        {
            var value8 = deviceID.ToString("00000000");
            var value16 = deviceID.ToString("0000000000000000");
            var value32 = deviceID.ToString("00000000000000000000000000000000");

            if (!string.IsNullOrEmpty(prefix))
            {
                value8 = string.Concat(prefix, value8.Substring(prefix.Length));
                value16 = string.Concat(prefix, value16.Substring(prefix.Length));
                value32 = string.Concat(prefix, value32.Substring(prefix.Length));
            }

            var devAddrValue = NetIdHelper.SetNwkIdPart(value8, netId);
            var result = new TestDeviceInfo
            {
                DeviceID = value16,
                GatewayID = gatewayID,
                SensorDecoder = sensorDecoder,
                AppSKey = value32,
                NwkSKey = value32,
                DevAddr = devAddrValue,
                ClassType = deviceClassType,
                Supports32BitFCnt = supports32BitFcnt
            };

            return result;
        }

        /// <summary>
        /// Creates a <see cref="TestDeviceInfo"/> with OTAA authentication
        /// </summary>
        /// <param name="deviceID">Device identifier. It will padded with 0's</param>
        public static TestDeviceInfo CreateOTAADevice(uint deviceID, string prefix = null, string gatewayID = null, string sensorDecoder = "DecoderValueSensor", char deviceClassType = 'A')
        {
            var value16 = deviceID.ToString("0000000000000000");
            var value32 = deviceID.ToString("00000000000000000000000000000000");

            if (!string.IsNullOrEmpty(prefix))
            {
                value16 = string.Concat(prefix, value16.Substring(prefix.Length));
                value32 = string.Concat(prefix, value32.Substring(prefix.Length));
            }

            var result = new TestDeviceInfo
            {
                DeviceID = value16,
                AppEUI = value16,
                AppKey = value32,
                GatewayID = gatewayID,
                SensorDecoder = sensorDecoder,
                ClassType = deviceClassType,
            };

            return result;
        }
    }
}