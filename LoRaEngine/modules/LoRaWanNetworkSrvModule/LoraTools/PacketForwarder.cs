//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using System.Net;
using System.Net.Sockets;


namespace LoRaTools
{
    /// <summary>
    /// Helper class that broadcasts UDP packets.
    /// Used to simulate a Semtech LoRaWAN packet forwarder device
    /// in unit tests.
    /// </summary>
    public class PacketForwarder
    {
        IPEndPoint m_endpoint;
        UdpClient m_client;

        /// <summary>
        /// Constructor is parameterized by ip address and port.
        /// </summary>
        /// <param name="ip">ip address to which packets will be broadcast</param>
        /// <param name="port">port to which packets will be broadcast</param>
        public PacketForwarder(string ip, int port)
        {
            m_endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
            m_client = new UdpClient();
        }

        /// <summary>
        /// Broadcasts a UDP packet made up of the specified bytes.
        /// The ip address and port are specified in the constructor.
        /// </summary>
        /// <param name="bytes"></param>
        public void Send(byte[] bytes)
        {
            m_client.Send(bytes, bytes.Length, m_endpoint);
        }


        /// <summary>
        /// Broadcasts a UDP packet corresponding to an IPacket.
        /// The ip address and port are specified in the constructor.
        /// </summary>
        /// <param name="bytes"></param>
        public void Send(IPacket packet)
        {
            var rawBytes = packet.GetRawWireBytes();
            Send(rawBytes);
        }
    }
}
