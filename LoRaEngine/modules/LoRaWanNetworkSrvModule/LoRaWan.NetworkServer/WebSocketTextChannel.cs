// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;

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
}
