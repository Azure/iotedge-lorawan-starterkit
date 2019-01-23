//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using LoRaTools.Utils;
using System;
using System.Collections.Generic;

namespace LoRaWan.Test.Shared
{
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

        /// <summary>
        /// Gets the desired properties for the <see cref="TestDeviceInfo"/>
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetDesiredProperties()
        {
            var desiredProperties = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(this.AppEUI))
                desiredProperties[nameof(AppEUI)] = this.AppEUI;

            if (!string.IsNullOrEmpty(this.AppKey))
                desiredProperties[nameof(AppKey)] = this.AppKey;

            if (!string.IsNullOrEmpty(this.GatewayID))
                desiredProperties[nameof(GatewayID)] = this.GatewayID;

            if (!string.IsNullOrEmpty(this.SensorDecoder))
                desiredProperties[nameof(SensorDecoder)] = this.SensorDecoder;                
            
            if (!string.IsNullOrEmpty(this.AppSKey))
                desiredProperties[nameof(AppSKey)] = this.AppSKey;

            if (!string.IsNullOrEmpty(this.NwkSKey))
                desiredProperties[nameof(NwkSKey)] = this.NwkSKey; 

            if (!string.IsNullOrEmpty(this.DevAddr))
                desiredProperties[nameof(DevAddr)] = this.DevAddr;
            
            return desiredProperties;
        }

        /// <summary>
        /// Creates a <see cref="TestDeviceInfo"/> with ABP authentication
        /// </summary>
        /// <param name="deviceID">Device identifier. It will padded with 0's</param>
        /// <param name="prefix"></param>
        /// <param name="gatewayID"></param>
        /// <param name="sensorDecoder"></param>
        /// <returns></returns>
        public static TestDeviceInfo CreateABPDevice(UInt32 deviceID, string prefix = null, string gatewayID = null, string sensorDecoder = "DecoderValueSensor",uint netId=1)
        {
            var value8 =  deviceID.ToString("00000000");
            var value16 = deviceID.ToString("0000000000000000");
            var value32 = deviceID.ToString("00000000000000000000000000000000");

            if (!string.IsNullOrEmpty(prefix))
            {
                value8 = string.Concat(prefix, value8.Substring(prefix.Length));
                value16 = string.Concat(prefix, value16.Substring(prefix.Length));
                value32 = string.Concat(prefix, value32.Substring(prefix.Length));
            }

            var devAddrValue = NetIdHelper.SetNwkIdPart(value8,netId);
            var result = new TestDeviceInfo
            {
                DeviceID = value16,
                GatewayID = gatewayID,
                SensorDecoder = sensorDecoder,
                AppSKey = value32,
                NwkSKey = value32,
                DevAddr = devAddrValue,
            };

            return result;
        }


        /// <summary>
        /// Creates a <see cref="TestDeviceInfo"/> with OTAA authentication
        /// </summary>
        /// <param name="deviceID">Device identifier. It will padded with 0's</param>
        /// <param name="prefix"></param>
        /// <param name="gatewayID"></param>
        /// <param name="sensorDecoder"></param>
        /// <returns></returns>
        public static TestDeviceInfo CreateOTAADevice(UInt32 deviceID, string prefix = null, string gatewayID = null, string sensorDecoder = "DecoderValueSensor")
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
            };

            return result;
        }
    }
}