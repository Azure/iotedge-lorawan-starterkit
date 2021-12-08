// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1008 // Enums should have zero value (use nullables instead)

namespace LoRaWan
{
    /// <summary>
    /// Fixed-bandwidth channel of either 125 KHz or 500 KHz (for uplink channels), and 500 KHz (for
    /// downlink channels).
    /// </summary>

    public enum Bandwidth
    {
        BW125 = 125, // KHz
        BW250 = 250, // KHz
        BW500 = 500, // KHz
    }

    public static class BandwidthExtensions
    {
        public static Hertz ToHertz(this Bandwidth bandwidth) => new((uint)bandwidth * 1000);
    }
}
