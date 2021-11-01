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
            this.channel = Channel.CreateUnbounded<Output>();
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
                    catch (Exception ex)
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

        public bool IsClosed => this.socket.State == WebSocketState.Closed;

        public async ValueTask SendAsync(string message, CancellationToken cancellationToken)
        {
            if (!this.isSendQueueProcessorRunning.ReadDirty())
                throw new InvalidOperationException();
            using var linkedTimeoutCancellationToken = new TimeoutLinkedCancellationToken(this.sendTimeout, cancellationToken);
            cancellationToken = linkedTimeoutCancellationToken.Token;
            var output = new Output(message, cancellationToken);
            await this.channel.Writer.WriteAsync(output, cancellationToken).ConfigureAwait(false);
            using var registration = cancellationToken.Register(() =>
                _ = output.TaskCompletionSource.TrySetCanceled(), useSynchronizationContext: false);
            _ = await output.TaskCompletionSource.Task.ConfigureAwait(false);
        }
    }
}
