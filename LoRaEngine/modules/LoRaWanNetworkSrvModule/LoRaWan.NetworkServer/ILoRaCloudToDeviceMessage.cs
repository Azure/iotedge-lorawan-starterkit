// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools;

    public interface ILoRaCloudToDeviceMessage
    {
        string DevEUI { get; }

        byte Fport { get; }

        bool Confirmed { get; }

        string MessageId { get; }

        /// <summary>
        /// Gets list of mac commands that are part of the message
        /// </summary>
        IList<MacCommand> MacCommands { get; }

        byte[] GetPayload();

        Task<bool> CompleteAsync();

        Task<bool> AbandonAsync();

        /// <summary>
        /// Identifies if the message is a valid LoRa downstream message
        /// </summary>
        /// <param name="errorMessage">Returns the error message in case it fails</param>
        /// <returns>True if the message is valid, false otherwise</returns>
        bool IsValid(out string errorMessage);
    }
}