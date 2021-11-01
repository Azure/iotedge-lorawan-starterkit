// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public static class WebSocketsRegistry
    {
        public static readonly TimeSpan DefaultPruningInterval = TimeSpan.FromMinutes(2);
    }

    public sealed class WebSocketsRegistry<T>
    {
        private readonly Dictionary<string, (IWebSocket<T> Object, WebSocketHandle<T> Handle)> sockets = new();
        private readonly ILogger? logger;

        public WebSocketsRegistry(ILogger<WebSocketsRegistry<T>>? logger) =>
            this.logger = logger;

        public Task RunPrunerAsync(CancellationToken cancellationToken) =>
            RunPrunerAsync(WebSocketsRegistry.DefaultPruningInterval, cancellationToken);

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

        public string[] Prune()
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

        public WebSocketHandle<T> this[string key]
        {
            get
            {
                lock (this.sockets)
                    return this.sockets[key].Handle;
            }
        }

        public WebSocketHandle<T> Register(string key, IWebSocket<T> socket)
        {
            lock (this.sockets)
            {
                if (this.sockets.TryGetValue(key, out var currentSocket) && socket == currentSocket.Object)
                    return currentSocket.Handle;
                var handle = new WebSocketHandle<T>(this, key);
                this.sockets[key] = (socket, handle);
                return handle;
            }
        }

        public IWebSocket<T>? Deregister(string key)
        {
            lock (this.sockets)
                return this.sockets.TryGetValue(key, out var currentSocket) ? currentSocket.Object: null;
        }

        internal async ValueTask SendAsync(string key, T message,
                                           CancellationToken cancellationToken)
        {
            IWebSocket<T> socket;
            lock (this.sockets)
                socket = this.sockets[key].Object;
            await socket.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }
}
