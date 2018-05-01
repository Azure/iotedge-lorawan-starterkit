using System;

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
                String.Format("Enter verbatim packet text, a packet number in the range 0..{0} or a blank line to exit.",
                              LoRaTools.PrerecordedPackets.GetPacketCount() - 1));
            Console.WriteLine("");

            LoRaTools.PacketForwarder forwarder = new LoRaTools.PacketForwarder(ip, port);

            while (true)
            {
                // Prompt for packet text.
                Console.Write("packet? ");
                Console.Out.Flush();            // TODO: REVIEW: flush not required on Windows. Check on Linux.
                var line = Console.ReadLine();

                // Exit on blank line.
                if (line.Length == 0)
                {
                    break;
                }

                // Otherwise, try to determine which packet to broadcast.

                LoRaTools.IPacket packet = null;

                if (LoRaTools.PacketValidator.IsLikelyValidLoRaWanPacket(line))
                {
                    // Input line represents complete text of the packet.
                    // Just build the packet from the input text.
                    packet = new LoRaTools.RecordedPacket(line);
                    Console.WriteLine("  ... broadcasting verbatim packet");
                }
                else
                {
                    Int32 n = 0;
                    if (Int32.TryParse(line, out n))
                    {
                        if (n >= 0 && n < LoRaTools.PrerecordedPackets.GetPacketCount())
                        {
                            // Input line represents a valid pre-recorded packet number.
                            // Look up the pre-recorded packet.
                            packet = LoRaTools.PrerecordedPackets.GetPacket(n);
                            Console.WriteLine(String.Format("  ... broadcasting pre-recorded packet {0}.", n));
                        }
                    }
                }

                if (packet != null)
                {
                    // We have a packet. Broadcast it.
                    forwarder.Send(packet);

                    // TODO: REVIEW: should we print out or log the raw packet
                    // text here for diagnostic purposes.
                }
                else
                {
                    // We couldn't figure out which packet was requested.
                    // Print help message.
                    Console.WriteLine("Invalid packet.");
                    PrintREPLHelp();
                }

                // TODO: catch and handle other errors here? At least set return code on failure.
            }
        }


        static void PrintREPLHelp()
        {
            Console.WriteLine("Valid options include");
            Console.WriteLine("  Complete LoRaWan packet text, starting with 24 hex digits");
            Console.WriteLine("     followed by JSON parameters and payload.");
            Console.WriteLine(
                String.Format("  Pre-recorded packet number in the range 0..{0}",
                                LoRaTools.PrerecordedPackets.GetPacketCount() - 1));
            Console.WriteLine("  Blank line to exit");
            Console.WriteLine("");
        }
    }
}
