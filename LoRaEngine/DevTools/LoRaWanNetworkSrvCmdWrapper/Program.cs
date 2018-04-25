using LoRaWan.NetworkServer;
using System;

namespace CmdWrapper
{
    class Program
    {
        static void Main(string[] args)
        {
            UdpServer udpServer = new UdpServer();
            udpServer.RunServer();

            Console.WriteLine("Server has been started...");
            Console.ReadLine();
        }
    }
}
