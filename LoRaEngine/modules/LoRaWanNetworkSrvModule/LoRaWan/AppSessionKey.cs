// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;

    /// <summary>
    /// Data encryption key (AppSKey) used for encryption and decryption of payload.
    /// </summary>
    public readonly struct AppSessionKey : IEquatable<AppSessionKey>
    {
        readonly UInt128 value;

        public AppSessionKey(UInt128 value) => this.value = value;

        public bool Equals(AppSessionKey other) => this.value == other.value;
        public override bool Equals(object obj) => obj is AppSessionKey other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => this.value.ToString();

        public static bool operator ==(AppSessionKey left, AppSessionKey right) => left.Equals(right);
        public static bool operator !=(AppSessionKey left, AppSessionKey right) => !left.Equals(right);
    }
}
