// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public sealed class WebSocketTextChannel : IWebSocket<string>
    {
        private sealed class Output
        {
            public Output(string message, CancellationToken cancellationToken) =>
                (Message, CancellationToken) = (message, cancellationToken);

            public string Message { get; }
            public CancellationToken CancellationToken { get; }
            public TaskCompletionSource<int> TaskCompletionSource { get; } = new();
        }

        private readonly WebSocket socket;
        private readonly Channel<Output> channel;
        private readonly Synchronized<bool> isSendQueueProcessorRunning = new(false);

        public WebSocketTextChannel(WebSocket socket)
        {
            this.socket = socket;
            this.channel = Channel.CreateUnbounded<Output>();
        }

        /// <summary>
        /// Reads all text messages arriving on the socket until a message indicating that the
        /// socket is closed has been received.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Thrown when a message type other than text is received.
        /// </exception>
        /// <remarks>
        /// The underlying socket is not closed.
        /// </remarks>
        public IAsyncEnumerator<string> ReadMessages(CancellationToken cancellationToken) =>
            ReadMessages(MemoryPool<byte>.Shared, 1024, cancellationToken);

        /// <summary>
        /// Reads all text messages arriving on the socket until a message indicating that the
        /// socket is closed has been received. Additional arguments specify the memory pool and
        /// buffer size to use for receiving messages.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Thrown when a message type other than text is received.
        /// </exception>
        /// <remarks>
        /// The underlying socket is not closed.
        /// </remarks>
        public async IAsyncEnumerator<string> ReadMessages(MemoryPool<byte> memoryPool, int minBufferSize, CancellationToken cancellationToken)
        {
            if (memoryPool == null) throw new ArgumentNullException(nameof(memoryPool));

            while (true)
            {
                ValueWebSocketReceiveResult result;
                using var buffer = memoryPool.Rent(minBufferSize);
                using var ms = new MemoryStream(buffer.Memory.Length);
                do
                {
                    result = await this.socket.ReceiveAsync(buffer.Memory, cancellationToken);
#pragma warning disable IDE0010 // Add missing cases (all are covered)
                    switch (result.MessageType)
#pragma warning restore IDE0010 // Add missing cases
                    {
                        case WebSocketMessageType.Close:
                            yield break;
                        case var type and not WebSocketMessageType.Text:
                            throw new NotSupportedException($"Invalid message type received: {type}");
                    }

                    ms.Write(buffer.Memory.Span[..result.Count]);
                }
                while (!result.EndOfMessage);

                ms.Position = 0;

                string input;
                using (var reader = new StreamReader(ms))
                    input = reader.ReadToEnd();
                yield return input;
            }
        }

        /// <remarks>
        /// If this method is called when a previous invocation has not completed then it throws
        /// <see cref="InvalidOperationException"/>.
        /// </remarks>
        public async Task ProcessSendQueueAsync(CancellationToken cancellationToken)
        {
            if (!this.isSendQueueProcessorRunning.Write(true))
                throw new InvalidOperationException();

            try
            {
                await foreach (var entry in this.channel.Reader.ReadAllAsync(cancellationToken))
                {
                    if (entry.CancellationToken.IsCancellationRequested)
                        continue;

                    var bytes = Encoding.UTF8.GetBytes(entry.Message);

                    try
                    {
                        await this.socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, entry.CancellationToken)
                                  .ConfigureAwait(false);
                        _ = entry.TaskCompletionSource.TrySetResult(default);
                    }
                    catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
                    {
                        // ignore and continue with next
                    }
                    catch (Exception ex)
                    {
                        _ = entry.TaskCompletionSource.TrySetException(ex);
                    }
                }
            }
            finally
            {
                _ = this.isSendQueueProcessorRunning.Write(false);
            }
        }

        public bool IsClosed => this.socket.State == WebSocketState.Closed;

        public ValueTask<string> ReceiveAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async ValueTask SendAsync(string message, CancellationToken cancellationToken)
        {
            if (!this.isSendQueueProcessorRunning.ReadDirty())
                throw new InvalidOperationException();
            using var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);
            cancellationToken = linkedTokenSource.Token;
            var output = new Output(message, cancellationToken);
            await this.channel.Writer.WriteAsync(output, cancellationToken).ConfigureAwait(false);
            using var registration = cancellationToken.Register(() =>
                _ = output.TaskCompletionSource.TrySetCanceled(), useSynchronizationContext: false);
            _ = await output.TaskCompletionSource.Task.ConfigureAwait(false);
        }
    }

    /*
    public struct TimeoutSource : IDisposable
    {
        private readonly CancellationTokenSource? cancellationTokenSource;
        private readonly CancellationTokenSource? linkedCancellationTokenSource;

        public TimeoutSource(CancellationTokenSource cancellationTokenSource, CancellationTokenSource? linkedCancellationTokenSource, CancellationToken cancellationToken)
        {
            this.cancellationTokenSource = cancellationTokenSource;
            this.linkedCancellationTokenSource = linkedCancellationTokenSource;
            CancellationToken = cancellationToken;
        }

        public static TimeoutSource Create(TimeSpan duration, CancellationToken cancellationToken)
        {
            CancellationTokenSource? cancellationTokenSource = null, linkedCancellationTokenSource = null;
            try
            {
                cancellationTokenSource = new CancellationTokenSource(duration);
                var cancellationToken = cancellationTokenSource.Token;
                if (cancellationToken.CanBeCanceled)
                {
                    linkedCancellationTokenSource =
                        CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token,
                                                                        cancellationToken);
                    cancellationToken = linkedCancellationTokenSource.Token;
                }
                var r = new TimeoutSource(cancellationTokenSource, linkedCancellationTokenSource, cancellationToken);
                cancellationTokenSource = linkedCancellationTokenSource = null;
                return r;
            }
            finally
            {
                linkedCancellationTokenSource?.Dispose();
                cancellationTokenSource?.Dispose();
            }
        }

        public CancellationToken CancellationToken { get; }

        public void Dispose()
        {
            this.linkedCancellationTokenSource?.Dispose();
            this.cancellationTokenSource.Dispose();
        }
    }
    */

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

        public ValueTask<T> ReceiveAsync(CancellationToken cancellationToken) =>
            this.registry.ReceiveAsync(this.key, cancellationToken);

        public ValueTask SendAsync(T message, CancellationToken cancellationToken) =>
            this.registry.SendAsync(this.key, message, cancellationToken);

        public override bool Equals(object? obj) =>
            ReferenceEquals(this, obj) || Equals(obj as WebSocketHandle<T>);

        public bool Equals(WebSocketHandle<T>? other) =>
            other is not null && this.registry.Equals(other.registry) && this.key == other.key;

        public override int GetHashCode() => HashCode.Combine(this.registry, this.key);
    }

    public interface IWebSocket<T>
    {
        bool IsClosed { get; }

        ValueTask<T> ReceiveAsync(CancellationToken cancellationToken);

        ValueTask SendAsync(T message, CancellationToken cancellationToken);
    }

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

        internal async ValueTask<T> ReceiveAsync(string key, CancellationToken cancellationToken)
        {
            IWebSocket<T> socket;
            lock (this.sockets)
                socket = this.sockets[key].Object;
            return await socket.ReceiveAsync(cancellationToken).ConfigureAwait(false);
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
