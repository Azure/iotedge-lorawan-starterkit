using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public class UdpServer
    {
        const int port = 1680;

        public async Task RunServer()
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
            using (var udpClient = new UdpClient(endPoint))
            {
                while (true)
                {
                    UdpReceiveResult receivedResults = await udpClient.ReceiveAsync();
                    MessageProcessor messageProcessor = new MessageProcessor();
                    Task.Run(() => messageProcessor.processMessage(receivedResults.Buffer));
                }
            }
        }
    }
}
