// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1819 // Properties should not return arrays

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The Physical Payload wrapper.
    /// </summary>
    public class PhysicalPayload
    {
        // Position in payload where the identifier is located (3)
        private const int PHYSICAL_IDENTIFIER_INDEX = 3;

        /// <summary>
        /// Get the type of a physical payload.
        /// </summary>
        public static PhysicalIdentifier GetIdentifierFromPayload(byte[] packet)
        {
            // Unknown if: packet is null or does not have the physical identifier byte
            if (packet == null || packet.Length < (PHYSICAL_IDENTIFIER_INDEX + 1))
                return PhysicalIdentifier.Unknown;

            return (PhysicalIdentifier)packet[3];
        }

        // case of inbound messages
        public PhysicalPayload(byte[] input, bool server = false)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));

            this.protocolVersion = input[0];
            Token = new byte[2];
            Array.Copy(input, 1, Token, 0, 2);
            Identifier = GetIdentifierFromPayload(input);

            if (!server)
            {
                // PUSH_DATA That packet type is used by the gateway mainly to forward the RF packets received, and associated metadata, to the server
                if (Identifier == PhysicalIdentifier.PushData)
                {
                    Array.Copy(input, 4, this.gatewayIdentifier, 0, 8);
                    Message = new byte[input.Length - 12];
                    Array.Copy(input, 12, Message, 0, input.Length - 12);
                }

                // PULL_DATA That packet type is used by the gateway to poll data from the server.
                if (Identifier == PhysicalIdentifier.PullData)
                {
                    Array.Copy(input, 4, this.gatewayIdentifier, 0, 8);
                }

                // TX_ACK That packet type is used by the gateway to send a feedback to the to inform if a downlink request has been accepted or rejected by the gateway.
                if (Identifier == PhysicalIdentifier.TxAck)
                {
                    Logger.Log($"Tx ack received from gateway", LogLevel.Debug);
                    Array.Copy(input, 4, this.gatewayIdentifier, 0, 8);
                    if (input.Length - 12 > 0)
                    {
                        Message = new byte[input.Length - 12];
                        Array.Copy(input, 12, Message, 0, input.Length - 12);
                    }
                }
            }
            else
            {
                // Case of message received on the server
                // PULL_RESP is an answer from the client to the server for Join requests for example
                if (Identifier == PhysicalIdentifier.PullResp)
                {
                    Message = new byte[input.Length - 4];
                    Array.Copy(input, 4, Message, 0, Message.Length);
                }
            }
        }

        // downlink transmission
        public PhysicalPayload(byte[] token, PhysicalIdentifier type, byte[] message)
        {
            // 0x01 PUSH_ACK That packet type is used by the server to acknowledge immediately all the PUSH_DATA packets received.
            // 0x04 PULL_ACK That packet type is used by the server to confirm that the network route is open and that the server can send PULL_RESP packets at any time.
            if (type is PhysicalIdentifier.PushAck or PhysicalIdentifier.PullAck)
            {
                Token = token;
                Identifier = type;
            }

            // 0x03 PULL_RESP That packet type is used by the server to send RF packets and  metadata that will have to be emitted by the gateway.
            else
            {
                Token = token;
                Identifier = type;
                if (message != null)
                {
                    Message = new byte[message.Length];
                    Array.Copy(message, 0, Message, 0, message.Length);
                }
            }
        }

        // 1 byte
        private readonly byte protocolVersion = 2;

        // 1-2 bytes
        public byte[] Token
        {
            get; set;
        }

        // 1 byte
        public PhysicalIdentifier Identifier { get; set; }

        // 8 bytes
        private readonly byte[] gatewayIdentifier = new byte[8];

        // 0-unlimited
        public byte[] Message { get; set; }

        public byte[] GetMessage()
        {
            var returnList = new List<byte>
            {
                this.protocolVersion
            };
            returnList.AddRange(Token);
            returnList.Add((byte)Identifier);
            if (Identifier is PhysicalIdentifier.PullData or
                PhysicalIdentifier.TxAck or
                PhysicalIdentifier.PushData)
            {
                returnList.AddRange(this.gatewayIdentifier);
            }
            if (Message != null)
                returnList.AddRange(Message);
            return returnList.ToArray();
        }

        // Method used by Simulator
        public byte[] GetSyncHeader(byte[] mac)
        {
            if (mac is null) throw new ArgumentNullException(nameof(mac));

            var buff = new byte[12];
            // first is the protocole version
            buff[0] = 2;
            // Random token
            buff[1] = Token[0];
            buff[2] = Token[1];
            // the identifier
            buff[3] = (byte)Identifier;
            // Then the MAC address specific to the server
            for (var i = 0; i < 8; i++)
                buff[4 + i] = mac[i];
            return buff;
        }
    }
}
