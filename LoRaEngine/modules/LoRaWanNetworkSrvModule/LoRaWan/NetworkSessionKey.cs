// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;

    /// <summary>
    /// Data encryption key (NwkSKey) used to "encode" the messages between the end nodes and the
    /// Network Server.
    /// </summary>
    public readonly struct NetworkSessionKey : IEquatable<NetworkSessionKey>
    {
        readonly UInt128 value;

        NetworkSessionKey(UInt128 value) => this.value = value;

        public bool Equals(NetworkSessionKey other) => this.value == other.value;
        public override bool Equals(object obj) => obj is NetworkSessionKey other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => this.value.ToString();

        public static bool operator ==(NetworkSessionKey left, NetworkSessionKey right) => left.Equals(right);
        public static bool operator !=(NetworkSessionKey left, NetworkSessionKey right) => !left.Equals(right);
    }
}
