using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public class IoTHubSender
    {
        static DeviceClient deviceClient;
        static string connStr = "HostName=testloriotv3hub.azure-devices.net;SharedAccessKeyName=device;SharedAccessKey=Wt1vLxOMHtCVTILfxzFBAyE3wWguBQOM8NM14dz8YVw=";

        public IoTHubSender()
        {
            deviceClient = DeviceClient.CreateFromConnectionString(connStr, "BE7A00000000888F", TransportType.Mqtt);
        }

        public async Task sendMessage(string msg)
        {
            var message = new Message(Encoding.ASCII.GetBytes(msg));
            await deviceClient.SendEventAsync(message);
        }
    }
}
