// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    /// <summary>
    /// A <see cref="IWebSocketWriter{T}"/> implementation for text messages that uses a queue to
    /// synchronize concurrent writes to a WebSocket.
    /// </summary>
    public sealed class WebSocketTextChannel : IWebSocketWriter<string>
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
        private readonly TimeSpan sendTimeout;

        public WebSocketTextChannel(WebSocket socket, TimeSpan sendTimeout)
        {
            this.socket = socket;
            this.sendTimeout = sendTimeout == Timeout.InfiniteTimeSpan || sendTimeout >= TimeSpan.Zero
                             ? sendTimeout
                             : throw new ArgumentOutOfRangeException(nameof(sendTimeout), sendTimeout, null);
            this.channel = Channel.CreateUnbounded<Output>(new UnboundedChannelOptions { SingleReader = true });
        }

        /// <summary>
        /// Asynchronously processes the send queue indefinitely until cancellation is requested.
        /// </summary>
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
                await foreach (var output in this.channel.Reader.ReadAllAsync(cancellationToken))
                {
                    if (output.CancellationToken.IsCancellationRequested)
                        continue;

                    var bytes = Encoding.UTF8.GetBytes(output.Message);

                    try
                    {
                        await this.socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, output.CancellationToken)
                                  .ConfigureAwait(false);
                        _ = output.TaskCompletionSource.TrySetResult(default);
                    }
                    catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
                    {
                        // ignore and continue with next
                    }
#pragma warning disable CA1031 // Do not catch general exception types (propagated to task)
                    catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        _ = output.TaskCompletionSource.TrySetException(ex);
                    }
                }
            }
            finally
            {
                _ = this.isSendQueueProcessorRunning.Write(false);
            }
        }

        /// <summary>
        /// Gets a Boolean indicating whether the WebSocket is closed.
        /// </summary>
        public bool IsClosed => this.socket.State == WebSocketState.Closed;

        /// <summary>
        /// Asynchronously sends a message on the WebSocket via a queue to synchronize concurrent
        /// writes.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The sending queue is not being processed. Call <see cref="ProcessSendQueueAsync"/>.
        /// </exception>
        /// <remarks>
        /// The asynchronous operation completes when the send queue has finished sending the
        /// message on the WebSocket. It cancels the operation if the message takes to long to be
        /// processed (the time-out duration specified to the constructor).
        /// </remarks>
        public async ValueTask SendAsync(string message, CancellationToken cancellationToken)
        {
            if (!this.isSendQueueProcessorRunning.ReadDirty())
                throw new InvalidOperationException();
            using var linkedCancellationTokens = cancellationToken.LinkWithTimeout(this.sendTimeout);
            cancellationToken = linkedCancellationTokens;
            var output = new Output(message, cancellationToken);
            await this.channel.Writer.WriteAsync(output, cancellationToken).ConfigureAwait(false);
            using var registration =
                cancellationToken.Register(static output => _ = ((Output?)output)!.TaskCompletionSource.TrySetCanceled(),
                                           output, useSynchronizationContext: false);
            _ = await output.TaskCompletionSource.Task.ConfigureAwait(false);
        }
    }
}
