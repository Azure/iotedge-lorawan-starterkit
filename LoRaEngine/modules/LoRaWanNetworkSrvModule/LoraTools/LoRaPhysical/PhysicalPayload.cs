using LoRaTools.LoRaPhysical;
using LoRaWan;
using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaTools
{

    public enum PhysicalIdentifier
    {
        PUSH_DATA, PUSH_ACK, PULL_DATA, PULL_RESP, PULL_ACK, TX_ACK, UNKNOWN=Byte.MaxValue
    }

    /// <summary>
    /// The Physical Payload wrapper
    /// </summary>
    public class PhysicalPayload
    {
        // Position in payload where the identifier is located (3)
        private const int PHYSICAL_IDENTIFIER_INDEX = 3;

        /// <summary>
        /// Get the type of a physical payload
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        public static PhysicalIdentifier GetIdentifierFromPayload(byte[] packet)
        {
            // Unknown if: packet is null or does not have the physical identifier byte
            if (packet == null || packet.Length < (PHYSICAL_IDENTIFIER_INDEX + 1))
                return PhysicalIdentifier.UNKNOWN;

            return (PhysicalIdentifier)packet[3];
        }

        //case of inbound messages
        public PhysicalPayload(byte[] input, bool server = false)
        {

            protocolVersion = input[0];
            Array.Copy(input, 1, token, 0, 2);
            identifier = GetIdentifierFromPayload(input);

            if (!server)
            {
                //PUSH_DATA That packet type is used by the gateway mainly to forward the RF packets received, and associated metadata, to the server
                if (identifier == PhysicalIdentifier.PUSH_DATA)
                {
                    Array.Copy(input, 4, gatewayIdentifier, 0, 8);
                    message = new byte[input.Length - 12];
                    Array.Copy(input, 12, message, 0, input.Length - 12);
                }

                //PULL_DATA That packet type is used by the gateway to poll data from the server.
                if (identifier == PhysicalIdentifier.PULL_DATA)
                {
                    Array.Copy(input, 4, gatewayIdentifier, 0, 8);
                }

                //TX_ACK That packet type is used by the gateway to send a feedback to the to inform if a downlink request has been accepted or rejected by the gateway.
                if (identifier == PhysicalIdentifier.TX_ACK)
                {
                    Logger.Log($"Tx ack received from gateway", Logger.LoggingLevel.Info);
                    Array.Copy(input, 4, gatewayIdentifier, 0, 8);
                    if (input.Length - 12 > 0)
                    {
                        message = new byte[input.Length - 12];
                        Array.Copy(input, 12, message, 0, input.Length - 12);
                    }
                }
            }
            else
            {
                // Case of message received on the server
                // PULL_RESP is an answer from the client to the server for Join requests for example
                if (identifier == PhysicalIdentifier.PULL_RESP)
                {
                    message = new byte[input.Length - 4];
                    Array.Copy(input, 4, message, 0, message.Length);
                }
                // TODO : implement other pull ones
            }
        }

        //downlink transmission
        public PhysicalPayload(byte[] _token, PhysicalIdentifier type, byte[] _message)
        {
            //0x01 PUSH_ACK That packet type is used by the server to acknowledge immediately all the PUSH_DATA packets received.
            //0x04 PULL_ACK That packet type is used by the server to confirm that the network route is open and that the server can send PULL_RESP packets at any time.
            if (type == PhysicalIdentifier.PUSH_ACK || type == PhysicalIdentifier.PULL_ACK)
            {
                token = _token;
                identifier = type;
            }

            //0x03 PULL_RESP That packet type is used by the server to send RF packets and  metadata that will have to be emitted by the gateway.
            else
            {
                token = _token;
                identifier = type;
                if (_message != null)
                {
                    message = new byte[_message.Length];
                    Array.Copy(_message, 0, message, 0, _message.Length);
                }
            }

        }

        //1 byte
        public byte protocolVersion = 2;
        //1-2 bytes
        public byte[] token = new byte[2];
        //1 byte
        public PhysicalIdentifier identifier;
        //8 bytes
        public byte[] gatewayIdentifier = new byte[8];
        //0-unlimited
        public byte[] message;

        public byte[] GetMessage()
        {
            List<byte> returnList = new List<byte>();
            returnList.Add(protocolVersion);
            returnList.AddRange(token);
            returnList.Add((byte)identifier);
            if (identifier == PhysicalIdentifier.PULL_DATA ||
                identifier == PhysicalIdentifier.TX_ACK ||
                identifier == PhysicalIdentifier.PUSH_DATA
                )
                returnList.AddRange(gatewayIdentifier);
            if (message != null)
                returnList.AddRange(message);
            return returnList.ToArray();
        }

        // Method used by Simulator
        public byte[] GetSyncHeader(byte[] mac)
        {
            byte[] buff = new byte[12];
            // first is the protocole version
            buff[0] = 2;
            // Random token
            buff[1] = token[0];
            buff[2] = token[1];
            // the identifier
            buff[3] = (byte)identifier;
            // Then the MAC address specific to the server
            for (int i = 0; i < 8; i++)
                buff[4 + i] = mac[i];
            return buff;
        }

    }

}

