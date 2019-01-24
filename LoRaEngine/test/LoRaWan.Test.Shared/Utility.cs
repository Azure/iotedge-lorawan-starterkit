// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace LoRaWan.Test.Shared
{
    using System.Collections.Generic;
    using System.Net.NetworkInformation;
    using System.Text;
    using Microsoft.Azure.EventHubs;

    /// <summary>
    /// Utility class
    /// </summary>
    public static class Utility
    {
        /// <summary>
        /// Gets mac adderss
        /// </summary>
        public static byte[] GetMacAddress()
        {
            string macAddresses = string.Empty;

            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus == OperationalStatus.Up)
                {
                    macAddresses = adapter.GetPhysicalAddress().ToString();
                    if (!string.IsNullOrEmpty(macAddresses))
                        break;
                }
            }

            return Encoding.UTF8.GetBytes(macAddresses);
        }

        // Tries to get device ID from EventData
        public static string GetDeviceId(this EventData message)
        {
            if (message.SystemProperties.TryGetValue("iothub-connection-device-id", out var deviceId))
            {
                return deviceId.ToString();
            }

            return string.Empty;
        }

        // Creates a IList from a single object
        public static IReadOnlyList<T> AsList<T>(this T value) => new T[] { value };
    }
}