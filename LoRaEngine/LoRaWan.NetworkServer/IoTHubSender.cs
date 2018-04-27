using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public class IoTHubSender : IDisposable
    {
        static DeviceClient deviceClient;

        public IoTHubSender(string deviceId, string connStr)
        {
            deviceClient = DeviceClient.CreateFromConnectionString(connStr, deviceId, TransportType.Mqtt);
        }

        public async Task sendMessage(string msg)
        {
            var message = new Message(Encoding.ASCII.GetBytes(msg));
            await deviceClient.SendEventAsync(message);
        }

        public void Dispose()
        {
            if(deviceClient != null)
            {
                try { deviceClient.CloseAsync().Wait(); } catch (Exception ex) { Console.WriteLine($"Device Client closing error: {ex.Message}"); }
                try { deviceClient.Dispose(); } catch (Exception ex) { Console.WriteLine($"Device Client disposing error: {ex.Message}"); }
            }
        }
    }
}
