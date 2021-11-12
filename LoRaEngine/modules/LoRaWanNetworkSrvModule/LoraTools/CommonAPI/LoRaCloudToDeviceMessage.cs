// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.CommonAPI
{
    using System.Collections.Generic;

    /// <summary>
    /// Defines the contract for a LoRa cloud to device message.
    /// </summary>
    public class LoRaCloudToDeviceMessage : ILoRaCloudToDeviceMessage
    {
        public string DevEUI { get; set; }

        public byte Fport { get; set; }

        /// <summary>
        /// Gets or sets payload as base64 string
        /// Use this to send bytes.
        /// </summary>
        public string RawPayload { get; set; }

        /// <summary>
        /// Gets or sets payload as string
        /// Use this to send text.
        /// </summary>
        public string Payload { get; set; }

        public bool Confirmed { get; set; }

        public string MessageId { get; set; }

        public IList<MacCommand> MacCommands { get; } = new List<MacCommand>();

        /// <summary>
        /// Gets if the cloud to device message has any payload data (mac commands don't count).
        /// </summary>
        protected bool HasPayload() => !string.IsNullOrEmpty(Payload) || !string.IsNullOrEmpty(RawPayload);

        /// <summary>
        /// Identifies if the message is a valid LoRa downstream message.
        /// </summary>
        /// <param name="errorMessage">Returns the error message in case it fails.</param>
        /// <returns>True if the message is valid, false otherwise.</returns>
        public virtual bool IsValid(out string errorMessage)
        {
            // ensure fport follows LoRa specification
            // 0    => reserved for mac commands
            // 224+ => reserved for future applications
            if (Fport >= LoRaFPort.ReservedForFutureAplications)
            {
                errorMessage = $"invalid fport '{Fport}' in cloud to device message '{MessageId}'";
                return false;
            }

            // fport 0 is reserved for mac commands
            if (Fport == LoRaFPort.MacCommand)
            {
                // Not valid if there is no mac command or there is a payload
                if ((MacCommands?.Count ?? 0) == 0 || HasPayload())
                {
                    errorMessage = $"invalid MAC command fport usage in cloud to device message '{MessageId}'";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }
    }
}
