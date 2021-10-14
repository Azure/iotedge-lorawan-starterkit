// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    /// <summary>
    /// Defines a loRa device request queue.
    /// </summary>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    // False positive, suffix is accurate.
    public interface ILoRaDeviceRequestQueue
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        /// <summary>
        /// Queues a request.
        /// </summary>
        void Queue(LoRaRequest request);
    }
}
