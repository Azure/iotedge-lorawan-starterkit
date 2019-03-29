// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    /// <summary>
    /// Defines the types of LoRa device class
    /// </summary>
    public enum LoRaDeviceClassType
    {
        // Class A only listens for downlinks after a uplink
        A,

        // Class B listens for downlinks according to a synchronized schedule
        // This class is not supported
        B,

        // Class C listen for downlinks forever
        C
    }
}
