using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public class UdpServer
    {
        const int port = 1680;
        public async void RunServer()
        {
            using (var udpClient = new UdpClient(port))
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
