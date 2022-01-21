// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1819 // Properties should not return arrays

namespace LoRaTools.LoRaMessage
{
    using System;
    using LoRaWan;

    /// <summary>
    /// The LoRaPayloadWrapper class wraps all the information any LoRa message share in common.
    /// </summary>
    public abstract class LoRaPayload
    {
        public MacMessageType MessageType => MHdr.MessageType;

        /// <summary>
        /// Gets or sets raw byte of the message.
        /// </summary>
        public byte[] RawMessage { get; set; }

        /// <summary>
        /// Gets or sets MAC header of the message.
        /// </summary>
        public MacHeader MHdr { get; set; }

        /// <summary>
        /// Gets or sets message Integrity Code.
        /// </summary>
        public Mic? Mic { get; set; }

        /// <summary>
        /// Gets or sets assigned Dev Address, TODO change??.
        /// </summary>
        public DevAddr DevAddr { get; set; }

        /// <summary>
        /// Gets the representation of the 32bit Frame counter to be used
        /// in the block if we are in 32bit mode.
        /// </summary>
        protected uint? Server32BitFcnt { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayload"/> class.
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding.
        /// </summary>
        protected LoRaPayload(byte[] inputMessage)
        {
            RawMessage = inputMessage ?? throw new ArgumentNullException(nameof(inputMessage));
            MHdr = new MacHeader(RawMessage[0]);
            // MIC 4 last bytes
            Mic = LoRaWan.Mic.Read(RawMessage.AsSpan(inputMessage.Length - 4, 4));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayload"/> class.
        /// This is used for downlink, The field will be computed at message creation.
        /// </summary>
        protected LoRaPayload()
        {
        }

        /// <summary>
        /// Method to calculate the encrypted version of the payload.
        /// </summary>
        /// <param name="key">the Application Secret Key.</param>
        /// <returns>the encrypted bytes.</returns>
        public abstract byte[] Serialize(AppSessionKey key);

        /// <summary>
        /// Method to calculate the encrypted version of the payload.
        /// </summary>
        /// <param name="key">The Network Session Key.</param>
        /// <returns>the encrypted bytes.</returns>
        public abstract byte[] Serialize(NetworkSessionKey key);

        public void Reset32BitFcnt() => Server32BitFcnt = null;
        public void Ensure32BitFcntValue(uint? server32bitFcnt) => Server32BitFcnt ??= server32bitFcnt;

        /// <summary>
        /// In 32bit mode, the server needs to infer the upper 16bits by observing
        /// the traffic between the device and the server. We keep a 32bit counter
        /// on the server and combine the upper 16bits with what the client sends us
        /// on the wire (lower 16bits). The result is the inferred counter as we
        /// assume it is on the client.
        /// </summary>
        /// <param name="payloadFcnt">16bits counter sent in the package.</param>
        /// <param name="fcnt">Current server frame counter holding 32bits.</param>
        /// <returns>The inferred 32bit framecounter value, with the higher 16bits holding the server
        /// observed counter information and the lower 16bits the information we got on the wire.</returns>
        public static uint InferUpper32BitsForClientFcnt(ushort payloadFcnt, uint fcnt)
        {
            const uint MaskHigher16 = 0xFFFF0000;

            // server represents the counter in 32bit so does the client, but only sends the lower 16bits
            // infering the upper 16bits from the current count
            var fcntServerUpper = fcnt & MaskHigher16;
            return fcntServerUpper | payloadFcnt;
        }

        public virtual bool RequiresConfirmation
            => false;
    }
}
