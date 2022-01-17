// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1712 // Do not prefix enum values with type name
#pragma warning disable CA1028 // Enum Storage should be Int32 (per protocol)

namespace LoRaWan
{
    using System;

    /// <summary>
    /// Represents the delay between uplink (TX) and first reception (RX) slot.
    /// </summary>
    /// <remarks>
    /// See section 5.7, "Setting delay between TX and RX (RXTimingSetupReq, RXTimingSetupAns)"
    /// of LoRaWAN 1.0.3 Specification for more details.
    /// </remarks>
    public enum RxDelay : byte
    {
        RxDelay0,  // delay of 1s (same as next)
        RxDelay1,  // delay of 1s
        RxDelay2,  // delay of 2s
        RxDelay3,  // delay of 3s
        RxDelay4,  // delay of 4s
        RxDelay5,  // delay of 5s
        RxDelay6,  // delay of 6s
        RxDelay7,  // delay of 7s
        RxDelay8,  // delay of 8s
        RxDelay9,  // delay of 9s
        RxDelay10, // delay of 10s
        RxDelay11, // delay of 11s
        RxDelay12, // delay of 12s
        RxDelay13, // delay of 13s
        RxDelay14, // delay of 14s
        RxDelay15, // delay of 15s
    }

    public static class RxDelayExtensions
    {
        /// <summary>
        /// Increments delay by a second.
        /// </summary>
        public static RxDelay Inc(this RxDelay delay) => (RxDelay)checked((byte)(delay + 1));

        /// <summary>
        /// Converts to integral seconds, if valid.
        /// </summary>
        public static int ToSeconds(this RxDelay delay) =>
            Enum.IsDefined(delay) ? (delay is RxDelay.RxDelay0 ? 1 : (int)delay) : throw new ArgumentException(null, nameof(delay));

        /// <summary>
        /// Converts to <see cref="TimeSpan"/>, if valid.
        /// </summary>
        public static TimeSpan ToTimeSpan(this RxDelay delay) => TimeSpan.FromSeconds(ToSeconds(delay));
    }
}
