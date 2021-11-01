// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    [DebuggerDisplay("{" + nameof(key) + "}")]
    public sealed class WebSocketHandle<T> : IEquatable<WebSocketHandle<T>>
    {
        private readonly WebSocketsRegistry<T> registry;
        private readonly string key;

        public WebSocketHandle(WebSocketsRegistry<T> registry, string key)
        {
            this.registry = registry;
            this.key = key;
        }

        public ValueTask SendAsync(T message, CancellationToken cancellationToken) =>
            this.registry.SendAsync(this.key, message, cancellationToken);

        public override bool Equals(object? obj) =>
            ReferenceEquals(this, obj) || Equals(obj as WebSocketHandle<T>);

        public bool Equals(WebSocketHandle<T>? other) =>
            other is not null && this.registry.Equals(other.registry) && this.key == other.key;

        public override int GetHashCode() => HashCode.Combine(this.registry, this.key);
    }
}
