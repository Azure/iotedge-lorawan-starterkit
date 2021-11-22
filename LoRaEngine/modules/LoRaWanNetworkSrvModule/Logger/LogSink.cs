// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    public interface ILogSink
    {
        LogLevel LogLevel { get; }
        void Log(LogLevel logLevel, string message);
    }

    public sealed class TcpLogSink : IDisposable, ILogSink
    {
        private readonly ILogger? logger;
        private readonly IPEndPoint serverEndpoint;
        private readonly Func<string, string>? formatter;
        private readonly int maxRetryAttempts;
        private readonly TimeSpan retryDelay;
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly Channel<string> channel;

        public static TcpLogSink Start(IPEndPoint serverEndPoint, LogLevel logLevel,
                                       int? maxRetryAttempts = null,
                                       TimeSpan? retryDelay = null,
                                       int? backlogCapacity = null,
                                       Func<string, string>? formatter = null,
                                       ILogger<TcpLogSink>? logger = null)
        {
            var sink = new TcpLogSink(serverEndPoint, logLevel, maxRetryAttempts, retryDelay, backlogCapacity, formatter, logger);
            _ = Task.Run(() => sink.SendAllLogMessagesAsync(sink.cancellationTokenSource.Token));
            return sink;
        }

        private TcpLogSink(IPEndPoint serverEndpoint, LogLevel logLevel,
                           int? maxRetryAttempts, TimeSpan? retryDelay, int? backlogCapacity,
                           Func<string, string>? formatter,
                           ILogger? logger)
        {
            LogLevel = logLevel;
            this.serverEndpoint = serverEndpoint;
            this.formatter = formatter;
            this.maxRetryAttempts = maxRetryAttempts ?? 6;
            this.retryDelay = retryDelay ?? TimeSpan.FromSeconds(10);
            this.channel = Channel.CreateBounded<string>(new BoundedChannelOptions(backlogCapacity ?? 1000)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest,
            });
            this.logger = logger;
        }

        public LogLevel LogLevel { get; }

        public void Dispose()
        {
            try
            {
                this.cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
                return; // bail out because this means object has already been disposed before
            }
            catch (AggregateException ex)
            {
                // "AggregateException" is thrown and contains all the exceptions thrown by the
                // registered callbacks on the associated "CancellationToken". However,
                // "IDisposable.Dispose" methods are not expected to throw exceptions thus it is
                // simply ignore here.

                Debug.WriteLine(ex);
            }

            this.cancellationTokenSource.Dispose();
        }

        private async Task SendAllLogMessagesAsync(CancellationToken cancellationToken)
        {
            var encoding = Encoding.UTF8;
            var buffers = ArrayPool<byte>.Shared;
            var client = new TcpClient();

            try
            {
                await foreach (var log in this.channel.Reader.ReadAllAsync(cancellationToken))
                {
                    var lines = log.IndexOfAny(NewLineChars) >= 0
                              ? new StringValues(SplitIntoLines(log).Append(string.Empty).ToArray()) // re-normalize
                              : new StringValues(log);

                    foreach (var line in lines)
                    {
                        for (var attempt = 1; ; attempt++)
                        {
                            try
                            {
                                if (!client.Connected)
                                {
                                    this.logger?.LogDebug("Connecting to log server at (attempt {Attempt}/{MaxRetryAttempts}): {ServerEndpoint}", attempt, this.maxRetryAttempts, this.serverEndpoint);
                                    await client.ConnectAsync(this.serverEndpoint.Address, this.serverEndpoint.Port);
                                }

                                var size = encoding.GetByteCount(line) + 2;
                                var buffer = buffers.Rent(size);
                                var bi = encoding.GetBytes(line, buffer);
                                buffer[bi++] = 13; // CR
                                buffer[bi++] = 10; // LF
                                Debug.Assert(bi == size);

                                try
                                {
                                    await client.GetStream().WriteAsync(buffer.AsMemory(..bi), cancellationToken);
                                    break; // don't retry on success
                                }
                                finally
                                {
                                    buffers.Return(buffer);
                                }
                            }
                            catch (Exception ex) when (ex is SocketException or IOException)
                            {
                                this.logger?.LogError(ex, "Error writing to the logging socket.");
                                client.Dispose();
                                client = new TcpClient();
                                if (attempt == this.maxRetryAttempts)
                                {
                                    this.logger?.LogWarning("Dropping message after all attempts failed: {Line}", line);
                                    break;
                                }
                                this.logger?.LogDebug("Waiting (delay = {RetryDelay}) before retrying to connecting to logging server.", this.retryDelay);
                                await Task.Delay(this.retryDelay, cancellationToken);
                            }
                        }
                    }
                }
            }
            finally
            {
                client.Dispose();
            }

            static IEnumerable<string> SplitIntoLines(string input)
            {
                using var reader = new StringReader(input);
                while (reader.ReadLine() is { } line)
                    yield return line;
            }
        }

        private static readonly char[] NewLineChars = { '\n', '\r' };

        public void Log(LogLevel logLevel, string message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            if (LogLevel > logLevel)
                return;

            // NOTE! The following will induce an "ObjectDisposedException" in two ways.
            // When this object is disposed, it cancels the cancellation token source then proceeds
            // to dispose it. If the cancellation token source is disposed then accessing its token
            // will "ObjectDisposedException". If it's been cancelled but not yet disposed then the
            // cancellation token query will return true and we'll throw "ObjectDisposedException".
            // Either way, if the object is being disposed while we end up here then an
            // "ObjectDisposedException" will get thrown. If by chance it gets disposed past the
            // following gate, then the worst that can happen is that another message can end up on
            // the channel, which is harmless.

            if (this.cancellationTokenSource.Token.IsCancellationRequested)
                throw new ObjectDisposedException(nameof(TcpLogSink));

            _ = this.channel.Writer.TryWrite(this.formatter?.Invoke(message) ?? message);
        }
    }
}
