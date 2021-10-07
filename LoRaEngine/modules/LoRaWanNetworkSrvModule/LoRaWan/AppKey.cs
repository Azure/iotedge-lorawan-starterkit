// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;

    /// <summary>
    /// Data encryption key used to "encode" the messages between the end nodes and the Application
    /// Server.
    /// </summary>
    public readonly struct AppKey : IEquatable<AppKey>
    {
        readonly UInt128 value;

        public AppKey(UInt128 value) => this.value = value;

        public bool Equals(AppKey other) => this.value == other.value;
        public override bool Equals(object obj) => obj is AppKey other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public override string ToString() => this.value.ToString();

        public static bool operator ==(AppKey left, AppKey right) => left.Equals(right);
        public static bool operator !=(AppKey left, AppKey right) => !left.Equals(right);
    }
}
