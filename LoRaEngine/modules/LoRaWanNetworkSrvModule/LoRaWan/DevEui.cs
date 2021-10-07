// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Global end-device ID in IEEE EUI-64 (64-bit Extended Unique Identifier) address space
    /// that uniquely identifies the end-device.
    /// </summary>
    /// <remarks>
    /// For OTAA devices, the DevEUI MUST be stored in the end-device before the Join procedure
    /// is executed. ABP devices do not need the DevEUI to be stored in the device itself, but
    /// it is recommended to do so.
    /// </remarks>
    readonly struct DevEui : IEquatable<DevEui>
    {
        readonly ulong value;

        public DevEui(ulong value) => this.value = value;

        public bool Equals(DevEui other) => this.value == other.value;
        public override bool Equals(object obj) => obj is DevEui other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => this.value.ToString("x16", CultureInfo.InvariantCulture);

        public static bool operator ==(DevEui left, DevEui right) => left.Equals(right);
        public static bool operator !=(DevEui left, DevEui right) => !left.Equals(right);
    }
}
