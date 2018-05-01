using System;
using System.Net;
using System.Net.Sockets;


namespace Simulator
{
    class Program
    {
        //Original IP: 10.0.28.34
        static void Main(string[] args)
        {
            string ip = "127.0.0.1";
            int port = 1680;

            Console.WriteLine("Welcome to the PacketForwarder Simulator");
            Console.WriteLine(String.Format("Broadcasting to {0}, port {1}.", ip, port));
            Console.WriteLine("");
            Console.WriteLine(
                String.Format("Enter packet number in the range 0..{0} or a blank line to quit.",
                              LoRaTools.PrerecordedPackets.GetPacketCount() - 1));
            Console.WriteLine("");

            LoRaTools.PacketForwarder forwarder = new LoRaTools.PacketForwarder(ip, port);

            while (true)
            {
                Console.Write("packet? ");
                Console.Out.Flush();            // TODO: REVIEW: flush not required on Windows. Check on Linux.
                var line = Console.ReadLine();

                if (line.Length == 0)
                {
                    break;
                }

                Int32 n = 0;
                if (Int32.TryParse(line, out n))
                {
                    try
                    {
                        var packet = LoRaTools.PrerecordedPackets.GetPacket(n);
                        var rawBytes = packet.GetRawWireBytes();

                        forwarder.Send(rawBytes);

                        Console.WriteLine(String.Format("  broadcast packet {0}", n));
                    }
                    catch (System.ArgumentException e)
                    {
                        Console.WriteLine("Invalid packet number.");
                        Console.WriteLine(
                            String.Format("Enter packet number in the range 0..{0} or a blank line to quit.",
                                          LoRaTools.PrerecordedPackets.GetPacketCount() - 1));
                        Console.WriteLine("");
                    }

                    // TODO: catch and handle other errors here? At least set return code on failure.
                }
            }
        }
    }
}
