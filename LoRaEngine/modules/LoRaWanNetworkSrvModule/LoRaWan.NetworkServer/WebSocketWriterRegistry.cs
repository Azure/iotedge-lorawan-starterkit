// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public static class WebSocketWriterRegistry
    {
        public static readonly TimeSpan DefaultPruningInterval = TimeSpan.FromMinutes(2);
    }

    public sealed class WebSocketWriterRegistry<TKey, TMessage>
        where TKey : notnull
        where TMessage : notnull
    {
        private readonly Dictionary<TKey, (IWebSocketWriter<TMessage> Object, WebSocketWriterHandle<TKey, TMessage> Handle)> sockets = new();
        private readonly ILogger? logger;

        public WebSocketWriterRegistry(ILogger<WebSocketWriterRegistry<TKey, TMessage>>? logger) =>
            this.logger = logger;

        public WebSocketWriterHandle<TKey, TMessage> Register(TKey key, IWebSocketWriter<TMessage> socketWriter)
        {
            lock (this.sockets)
            {
                if (this.sockets.TryGetValue(key, out var currentSocket) && socketWriter == currentSocket.Object)
                    return currentSocket.Handle;
                var handle = new WebSocketWriterHandle<TKey, TMessage>(this, key);
                this.sockets[key] = (socketWriter, handle);
                return handle;
            }
        }

        public IWebSocketWriter<TMessage>? Deregister(TKey key)
        {
            lock (this.sockets)
                return this.sockets.TryGetValue(key, out var currentSocket) ? currentSocket.Object : null;
        }

        public async ValueTask SendAsync(TKey key, TMessage message, CancellationToken cancellationToken)
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

        public Task RunPrunerAsync(CancellationToken cancellationToken) =>
            RunPrunerAsync(WebSocketWriterRegistry.DefaultPruningInterval, cancellationToken);

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
    }
}
