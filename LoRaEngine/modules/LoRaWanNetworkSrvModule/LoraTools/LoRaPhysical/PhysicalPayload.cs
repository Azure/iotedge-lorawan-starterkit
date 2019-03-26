// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

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
        public static PhysicalIdentifier GetIdentifierFromPayload(byte[] packet)
        {
            // Unknown if: packet is null or does not have the physical identifier byte
            if (packet == null || packet.Length < (PHYSICAL_IDENTIFIER_INDEX + 1))
                return PhysicalIdentifier.UNKNOWN;

            return (PhysicalIdentifier)packet[3];
        }

        // case of inbound messages
        public PhysicalPayload(byte[] input, bool server = false)
        {
            this.protocolVersion = input[0];
            this.Token = new byte[2];
            Array.Copy(input, 1, this.Token, 0, 2);
            this.Identifier = GetIdentifierFromPayload(input);

            if (!server)
            {
                // PUSH_DATA That packet type is used by the gateway mainly to forward the RF packets received, and associated metadata, to the server
                if (this.Identifier == PhysicalIdentifier.PUSH_DATA)
                {
                    Array.Copy(input, 4, this.gatewayIdentifier, 0, 8);
                    this.Message = new byte[input.Length - 12];
                    Array.Copy(input, 12, this.Message, 0, input.Length - 12);
                }

                // PULL_DATA That packet type is used by the gateway to poll data from the server.
                if (this.Identifier == PhysicalIdentifier.PULL_DATA)
                {
                    Array.Copy(input, 4, this.gatewayIdentifier, 0, 8);
                }

                // TX_ACK That packet type is used by the gateway to send a feedback to the to inform if a downlink request has been accepted or rejected by the gateway.
                if (this.Identifier == PhysicalIdentifier.TX_ACK)
                {
                    Logger.Log($"Tx ack received from gateway", LogLevel.Debug);
                    Array.Copy(input, 4, this.gatewayIdentifier, 0, 8);
                    if (input.Length - 12 > 0)
                    {
                        this.Message = new byte[input.Length - 12];
                        Array.Copy(input, 12, this.Message, 0, input.Length - 12);
                    }
                }
            }
            else
            {
                // Case of message received on the server
                // PULL_RESP is an answer from the client to the server for Join requests for example
                if (this.Identifier == PhysicalIdentifier.PULL_RESP)
                {
                    this.Message = new byte[input.Length - 4];
                    Array.Copy(input, 4, this.Message, 0, this.Message.Length);
                }
            }
        }

        // downlink transmission
        public PhysicalPayload(byte[] token, PhysicalIdentifier type, byte[] message)
        {
            // 0x01 PUSH_ACK That packet type is used by the server to acknowledge immediately all the PUSH_DATA packets received.
            // 0x04 PULL_ACK That packet type is used by the server to confirm that the network route is open and that the server can send PULL_RESP packets at any time.
            if (type == PhysicalIdentifier.PUSH_ACK || type == PhysicalIdentifier.PULL_ACK)
            {
                this.Token = token;
                this.Identifier = type;
            }

            // 0x03 PULL_RESP That packet type is used by the server to send RF packets and  metadata that will have to be emitted by the gateway.
            else
            {
                this.Token = token;
                this.Identifier = type;
                if (message != null)
                {
                    this.Message = new byte[message.Length];
                    Array.Copy(message, 0, this.Message, 0, message.Length);
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
            List<byte> returnList = new List<byte>
            {
                this.protocolVersion
            };
            returnList.AddRange(this.Token);
            returnList.Add((byte)this.Identifier);
            if (this.Identifier == PhysicalIdentifier.PULL_DATA ||
                this.Identifier == PhysicalIdentifier.TX_ACK ||
                this.Identifier == PhysicalIdentifier.PUSH_DATA)
                returnList.AddRange(this.gatewayIdentifier);
            if (this.Message != null)
                returnList.AddRange(this.Message);
            return returnList.ToArray();
        }

        // Method used by Simulator
        public byte[] GetSyncHeader(byte[] mac)
        {
            byte[] buff = new byte[12];
            // first is the protocole version
            buff[0] = 2;
            // Random token
            buff[1] = this.Token[0];
            buff[2] = this.Token[1];
            // the identifier
            buff[3] = (byte)this.Identifier;
            // Then the MAC address specific to the server
            for (int i = 0; i < 8; i++)
                buff[4 + i] = mac[i];
            return buff;
        }
    }
}
