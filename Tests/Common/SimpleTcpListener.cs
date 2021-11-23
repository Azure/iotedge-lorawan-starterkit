// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public sealed class SimpleTcpListener : IDisposable
    {
        public sealed class Context
        {
            private readonly TcpClient client;

            public Context(TcpClient client) => this.client = client;

            public EndPoint? RemoteEndPoint => this.client.Client.RemoteEndPoint;
            public EndPoint? LocalEndPoint => this.client.Client.LocalEndPoint;
        }

        private TcpListener? listener;

        private SimpleTcpListener(TcpListener listener) =>
            this.listener = listener;

        public static SimpleTcpListener Start(int port, Func<Context, NetworkStream, Task> processor) =>
            Start(port, null, processor);

        public static SimpleTcpListener Start(int port, int? backlog,
                                              Func<Context, NetworkStream, Task> processor,
                                              ILogger<SimpleTcpListener>? logger = null)
        {
            var listener = TcpListener.Create(port);
            if (backlog is { } someBacklog)
                listener.Start(someBacklog);
            else
                listener.Start();
            _ = ListenAsync();
            return new SimpleTcpListener(listener);

            async Task ListenAsync()
            {
                while (true)
                {
                    try
                    {
                        _ = OnProcessAsync(await listener.AcceptTcpClientAsync().ConfigureAwait(false));
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        logger?.LogError(ex, "Error accepting client connection.");
                    }
                }

                async Task OnProcessAsync(TcpClient client)
                {
                    try
                    {
                        await Task.Yield(); // Ensure remaining does not run on caller stack.

                        var stream = client.GetStream();
                        await using (stream.ConfigureAwait(false))
                            await processor(new Context(client), stream).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Error while processing data from the client.");
                    }
                    finally
                    {
                        client.Dispose();
                    }
                }
            }
        }

        public void Dispose()
        {
            var listener = this.listener;
            if (listener is null || Interlocked.CompareExchange(ref this.listener, null, listener) is null)
                return;
            listener.Stop();
        }
    }
}
