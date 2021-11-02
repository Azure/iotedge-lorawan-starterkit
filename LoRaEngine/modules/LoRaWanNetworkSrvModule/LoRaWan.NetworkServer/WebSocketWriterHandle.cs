// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public static class WebSocketWriterHandle
    {
        public static WebSocketWriterHandle<TKey, TMessage>
            Create<TKey, TMessage>(WebSocketWriterRegistry<TKey, TMessage> registry, TKey key)
            where TKey : notnull
            where TMessage : notnull =>
            new(registry, key);
    }

    [DebuggerDisplay("{" + nameof(key) + "}")]
    public sealed class WebSocketWriterHandle<TKey, TMessage> :
        IEquatable<WebSocketWriterHandle<TKey, TMessage>>
        where TKey : notnull
        where TMessage : notnull
    {
        private readonly WebSocketWriterRegistry<TKey, TMessage> registry;
        private readonly TKey key;

        public WebSocketWriterHandle(WebSocketWriterRegistry<TKey, TMessage> registry, TKey key)
        {
            this.registry = registry;
            this.key = key;
        }

        public ValueTask SendAsync(TMessage message, CancellationToken cancellationToken) =>
            this.registry.SendAsync(this.key, message, cancellationToken);

        public override bool Equals(object? obj) =>
            ReferenceEquals(this, obj) || Equals(obj as WebSocketWriterHandle<TKey, TMessage>);

        public bool Equals(WebSocketWriterHandle<TKey, TMessage>? other) =>
            other is not null && this.registry.Equals(other.registry) && EqualityComparer<TKey>.Default.Equals(this.key, other.key);

        public override int GetHashCode() => HashCode.Combine(this.registry, this.key);
    }
}
