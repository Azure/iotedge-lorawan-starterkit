// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.CommonAPI
{
    using System.Collections.Generic;

    /// <summary>
    /// Defines the data contract for cloud to device messages
    /// </summary>
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

        /// <summary>
        /// Identifies if the message is a valid LoRa downstream message
        /// </summary>
        /// <param name="errorMessage">Returns the error message in case it fails</param>
        /// <returns>True if the message is valid, false otherwise</returns>
        bool IsValid(out string errorMessage);
    }
}
