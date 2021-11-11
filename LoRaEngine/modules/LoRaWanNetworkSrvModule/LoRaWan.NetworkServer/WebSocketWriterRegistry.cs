// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public static class WebSocketWriterRegistry
    {
        public static readonly TimeSpan DefaultPruningInterval = TimeSpan.FromMinutes(2);
    }

    public partial class WebSocketWriterRegistry<TKey, TMessage>
    {
        [DebuggerDisplay("{" + nameof(key) + "}")]
        private sealed class Handle : IWebSocketWriterHandle<TMessage>
        {
            private readonly WebSocketWriterRegistry<TKey, TMessage> registry;
            private readonly TKey key;

            public Handle(WebSocketWriterRegistry<TKey, TMessage> registry, TKey key)
            {
                this.registry = registry;
                this.key = key;
            }

            public ValueTask SendAsync(TMessage message, CancellationToken cancellationToken) =>
                this.registry.SendAsync(this.key, message, cancellationToken);

            bool IEquatable<IWebSocketWriterHandle<TMessage>>.Equals(IWebSocketWriterHandle<TMessage>? other) =>
                Equals(other as Handle);

            public override bool Equals(object? obj) =>
                ReferenceEquals(this, obj) || Equals(obj as Handle);

            private bool Equals(Handle? other) =>
                other is not null && this.registry == other.registry
                                  && EqualityComparer<TKey>.Default.Equals(this.key, other.key);

            public override int GetHashCode() => HashCode.Combine(this.registry, this.key);
        }
    }

    /// <summary>
    /// A registry providing virtual handles over WebSocket writer objects.
    /// </summary>
    public sealed partial class WebSocketWriterRegistry<TKey, TMessage>
        where TKey : notnull
        where TMessage : notnull
    {
        private readonly Dictionary<TKey, (IWebSocketWriter<TMessage> Object, Handle Handle)> sockets = new();
        private readonly ILogger? logger;

        public WebSocketWriterRegistry(ILogger<WebSocketWriterRegistry<TKey, TMessage>>? logger) =>
            this.logger = logger;

        /// <summary>
        /// Attempts to retrieve the socket writer handle for the given key.
        /// </summary>
        public bool TryGetHandle(TKey key, [NotNullWhen(true)] out IWebSocketWriterHandle<TMessage>? handle)
        {
            lock (this.sockets)
            {
                if (this.sockets.TryGetValue(key, out var entry))
                {
                    handle = entry.Handle;
                    return true;
                }
                else
                {
                    handle = default;
                    return false;
                }
            }
        }

        /// <summary>
        /// Checks whether the socket writer associated with the given key
        /// is flagged as open. Actual connectivity is not checked.
        /// </summary>
        /// <remarks>
        /// Implementation does not throw when key is not present in the underlying dictionary.
        /// </remarks>
        public bool IsSocketWriterOpen(TKey key)
            => TryGetWriter(key, out var socketWriter) && !socketWriter.IsClosed;

        /// <summary>
        /// Registers a socket writer under a key, returning a handle to the writer.
        /// </summary>
        /// <remarks>
        /// This method is idempotent.
        /// </remarks>
        public IWebSocketWriterHandle<TMessage> Register(TKey key, IWebSocketWriter<TMessage> socketWriter)
        {
            lock (this.sockets)
            {
                Handle handle;

                if (this.sockets.TryGetValue(key, out var current))
                {
                    handle = current.Handle;
                    if (socketWriter == current.Object)
                        return handle;
                }
                else
                {
                    handle = new Handle(this, key);
                }

                this.sockets[key] = (socketWriter, handle);
                return handle;
            }
        }

        /// <summary>
        /// Deregisters any socket writer associated with a key, return the associated writer if one
        /// was previously registered.
        /// </summary>
        public IWebSocketWriter<TMessage>? Deregister(TKey key)
        {
            lock (this.sockets)
            {
                if (this.sockets.TryGetValue(key, out var current))
                {
                    _ = this.sockets.Remove(key);
                    return current.Object;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Sends a message to the socket writer associated with the given key.
        /// </summary>
        /// <remarks>
        /// If the send fails because the socket has been closed prematurely then the socket writer
        /// is unregistered before this method returns.
        /// </remarks>
        private async ValueTask SendAsync(TKey key, TMessage message, CancellationToken cancellationToken)
        {
            IWebSocketWriter<TMessage> socketWriter;

            lock (this.sockets)
                socketWriter = this.sockets[key].Object;

            try
            {
                await socketWriter.SendAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                _ = Deregister(key);
            }
        }

        /// <summary>
        /// Removes closed sockets at default interval periods until cancellation is requested.
        /// </summary>
        public Task RunPrunerAsync(CancellationToken cancellationToken) =>
            RunPrunerAsync(WebSocketWriterRegistry.DefaultPruningInterval, cancellationToken);

        /// <summary>
        /// Removes closed sockets at a given a interval period until cancellation is requested.
        /// </summary>
        public Task RunPrunerAsync(TimeSpan interval, CancellationToken cancellationToken)
        {
            return Task.Run(LoopAsync, cancellationToken);

            async Task LoopAsync()
            {
                while (true)
                {
                    await Task.Delay(interval, cancellationToken).ConfigureAwait(false);

                    var prunedKeys = Prune();

                    this.logger?.LogDebug($"Pruned closed WebSocket connections: {string.Join(",", prunedKeys)}");
                }
            }
        }

        /// <summary>
        /// Removes all closed sockets, returning the keys of those removed.
        /// </summary>
        public TKey[] Prune()
        {
            lock (this.sockets)
            {
                var keys = this.sockets.Where(e => e.Value.Object.IsClosed)
                                       .Select(e => e.Key)
                                       .ToArray();

                foreach (var key in keys)
                    _ = this.sockets.Remove(key);

                return keys;
            }
        }

        private bool TryGetWriter(TKey key, [NotNullWhen(true)] out IWebSocketWriter<TMessage>? socketWriter)
        {
            lock (this.sockets)
            {
                if (this.sockets.TryGetValue(key, out var entry))
                {
                    socketWriter = entry.Object;
                    return true;
                }
                else
                {
                    socketWriter = default;
                    return false;
                }
            }
        }
    }
}
