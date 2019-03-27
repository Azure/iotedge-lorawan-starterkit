// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Helpers
{
    using System;

    public static class NetIdHelper
    {
        /// <summary>
        /// Set the NetworkId Part of a devAddr with a given network Id.
        /// </summary>
        public static string SetNwkIdPart(string currentDevAddr, string networkId, ConfigurationHelper configurationHelper)
        {
            string netIdLastByte;
            uint netId = 1;

            if (ValidationHelper.ValidateHexStringTwinProperty(configurationHelper.NetId, 3, out _))
            {
                netIdLastByte = configurationHelper.NetId.Substring(configurationHelper.NetId.Length - 2, 2);
                netId = uint.Parse(netIdLastByte, System.Globalization.NumberStyles.HexNumber);
            }

            if (!string.IsNullOrEmpty(networkId))
            {
                if (ValidationHelper.ValidateHexStringTwinProperty(networkId, 3, out _))
                {
                    netIdLastByte = networkId.Substring(networkId.Length - 2, 2);
                    netId = uint.Parse(netIdLastByte, System.Globalization.NumberStyles.HexNumber);
                }
            }

            return SetNwkIdPart(currentDevAddr, netId);
        }

        public static string SetNwkIdPart(string currentDevAddr, uint networkId)
        {
            byte[] netId = BitConverter.GetBytes(networkId);
            int nwkPart = netId[0] << 1;
            byte[] deviceIdBytes = ConversionHelper.StringToByteArray(currentDevAddr);
            deviceIdBytes[0] = (byte)((nwkPart & 0b11111110) | (deviceIdBytes[0] & 0b00000001));
            return ConversionHelper.ByteArrayToHexString(deviceIdBytes);
        }

        public static uint GetNwkIdPart(string devAddr)
        {
            var devAddrBytes = ConversionHelper.StringToByteArray(devAddr);
            return (uint)(devAddrBytes[0] >> 1);
        }
    }
}
