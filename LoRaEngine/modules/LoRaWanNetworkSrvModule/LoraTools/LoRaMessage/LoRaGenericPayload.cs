using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaTools.LoRaMessage
{
    /// <summary>
    /// The LoRaPayloadWrapper class wraps all the information any LoRa message share in common
    /// </summary>
    public abstract class LoRaGenericPayload
    {
        /// <summary>
        /// raw byte of the message
        /// </summary>
        public byte[] RawMessage { get; set; }

        /// <summary>
        /// MACHeader of the message
        /// </summary>
        public Memory<byte> Mhdr { get; set; }

        /// <summary>
        /// Message Integrity Code
        /// </summary>
        public Memory<byte> Mic { get; set; }

        /// <summary>
        /// Assigned Dev Address, TODO change??
        /// </summary>
        public byte[] DevAddr { get; set; }

        /// <summary>
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaGenericPayload(byte[] inputMessage)
        {
            RawMessage = inputMessage;       
            Mhdr = new Memory<byte>(RawMessage,0,1);

            // MIC 4 last bytes
            byte[] mic = new byte[4];
            Array.Copy(inputMessage, inputMessage.Length - 4, mic, 0, 4);
            this.Mic = new Memory<byte>(RawMessage, inputMessage.Length - 4,4);
        }

        /// <summary>
        /// This is used for downlink, The field will be computed at message creation
        /// </summary>
        public LoRaGenericPayload()
        {
        }

        public LoRaMessageAdapter GetLoRaMessage()
        {
            LoRaMessageAdapter messageAdapter = new LoRaMessageAdapter(this);
            return messageAdapter;
        }

        /// <summary>
        /// Method to take the different fields and assemble them in the message bytes
        /// </summary>
        /// <returns>the message bytes</returns>
        public abstract byte[] GetByteMessage();
    }
}
