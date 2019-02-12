// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using LoRaTools.LoRaPhysical;

    /// <summary>
    /// Defines a loRa device request queue
    /// </summary>
    public interface ILoRaDeviceRequestQueue
    {
        /// <summary>
        /// Queues a request
        /// </summary>
        void Queue(LoRaRequest request);
    }
}