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
        /// Method to check a Mic.
        /// </summary>
        /// <param name="key">The Network Secret Key.</param>
        /// <param name="server32BitFcnt">Explicit 32bit count to use for calculating the block.</param>
        public abstract bool CheckMic(NetworkSessionKey key, uint? server32BitFcnt = null);

        /// <summary>
        /// Method to check a Mic.
        /// </summary>
        /// <param name="key">The App Key.</param>
        public abstract bool CheckMic(AppKey key);

        public virtual bool RequiresConfirmation => false;
    }
}
