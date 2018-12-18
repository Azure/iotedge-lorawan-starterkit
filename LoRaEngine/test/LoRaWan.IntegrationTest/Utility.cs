
using System.Net.NetworkInformation;
using System.Text;
using Microsoft.Azure.EventHubs;

namespace LoRaWan.IntegrationTest
{
    internal static class Utility
    {
        internal static byte[] GetMacAddress()
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
        internal static string GetDeviceId(this EventData message)
        {
            if (message.SystemProperties.TryGetValue("iothub-connection-device-id", out var deviceId))
            {
                return deviceId.ToString();
            }

            return string.Empty;
        }
    }
}