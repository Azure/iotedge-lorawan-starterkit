using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaTools.Utils
{
    public static class NetIdHelper
    {
        /// <summary>
        /// Set the NetworkId Part of a devAddr with a given network Id.
        /// </summary>
        /// <param name="currentDevAddr"></param>
        /// <param name="networkId"></param>
        /// <returns></returns>
        public static string SetNwkIdPart(string currentDevAddr, uint networkId)
        {
            byte[] netId = BitConverter.GetBytes(networkId);
            int nwkPart = (netId[0] << 1);
            byte[] deviceIdBytes = ConversionHelper.StringToByteArray(currentDevAddr);
            deviceIdBytes[0] = (byte)((nwkPart&0b11111110)|(deviceIdBytes[0]&0b00000001));
            return ConversionHelper.ByteArrayToString(deviceIdBytes);
        }

        public static uint GetNwkIdPart(string devAddr)
        {
            var devAddrBytes= ConversionHelper.StringToByteArray(devAddr);
            return (uint)(devAddrBytes[0] >> 1);
        }
    }
}
