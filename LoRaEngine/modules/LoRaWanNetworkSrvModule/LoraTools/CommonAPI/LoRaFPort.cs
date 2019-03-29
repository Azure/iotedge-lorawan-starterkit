// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.CommonAPI
{
    /// <summary>
    /// LoRa FPort information
    /// </summary>
    public static class LoRaFPort
    {
        /// <summary>
        /// Starting Fport value reserved for future applications
        /// </summary>
        public const byte ReservedForFutureAplications = 224;

        /// <summary>
        /// Fport value reserved for mac commands
        /// </summary>
        public const int MacCommand = 0;
    }
}