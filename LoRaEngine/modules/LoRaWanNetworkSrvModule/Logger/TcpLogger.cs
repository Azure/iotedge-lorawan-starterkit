// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Logger
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;
    using LoRaWan;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Configuration;

    /// <summary>
    /// TcpLogger logs to a TCP endpoint that is listening on this endpoint. This is used only for E2E tests.
    /// TcpLogger does not support category names or event IDs.
    /// </summary>
    internal sealed class TcpLogger : ILogger
    {
        private readonly TcpLogSink logSink;
        private readonly TcpLoggerConfiguration loggerConfiguration;

        public TcpLogger(TcpLogSink logSink,
                         TcpLoggerConfiguration loggerConfiguration)
        {
            this.logSink = logSink;
            this.loggerConfiguration = loggerConfiguration ?? throw new ArgumentNullException(nameof(loggerConfiguration));
        }

        /// <summary>
        /// Gets or sets the external scope provider.
        /// </summary>
        internal IExternalScopeProvider? ExternalScopeProvider { get; set; }

        public IDisposable BeginScope<TState>(TState state) =>
            ExternalScopeProvider is { } scopeProvider ? scopeProvider.Push(state) : NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= this.loggerConfiguration.LogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _ = formatter ?? throw new ArgumentNullException(nameof(formatter));

            if (!IsEnabled(logLevel))
                return;

            var formattedMessage = LoggerHelper.AddScopeInformation(ExternalScopeProvider, formatter(state, exception), ":");

            this.logSink.Log(logLevel, formattedMessage);
        }
    }

    public static class TcpLoggerExtensions
    {
        public static ILoggingBuilder AddTcpLogger(this ILoggingBuilder builder, TcpLoggerConfiguration configuration, ILogger<TcpLogSink>? tcpLogSinkLogger = null)
        {
            _ = builder ?? throw new ArgumentNullException(nameof(builder));

            builder.AddConfiguration();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, TcpLoggerProvider>(_ => new TcpLoggerProvider(Init(configuration, tcpLogSinkLogger), configuration)));
            return builder;

            static TcpLogSink Init(TcpLoggerConfiguration configuration, ILogger<TcpLogSink>? tcpLogSinkLogger = null)
            {
                if (configuration == null) throw new ArgumentNullException(nameof(configuration));

                IPEndPoint? endPoint = null;

#pragma warning disable format
            if (configuration is { LogToTcpAddress: { Length: > 0 } address,
                                   LogToTcpPort   : var port and > 0 })
#pragma warning restore format
            {
                    endPoint = Resolve(address) is { } someAddress
                             ? new IPEndPoint(someAddress, port)
                             : null;
                }

                return endPoint is { } someEndPoint
                    ? TcpLogSink.Start(someEndPoint,
                                       formatter: string.IsNullOrEmpty(configuration.GatewayId)
                                                  ? null
                                                  : msg => $"[{configuration.GatewayId}] {msg}",
                                       logger: tcpLogSinkLogger)
                    : throw new InvalidOperationException($"TCP endpoint is incorrectly configured. Address is '{configuration.LogToTcpAddress}' and port is '{configuration.LogToTcpPort}'");

                IPAddress? Resolve(string address)
                {
                    if (IPAddress.TryParse(address, out var ipAddress))
                    {
                        return ipAddress;
                    }

                    string message;

                    try
                    {
                        // try to parse the address as dns
                        var addresses = Dns.GetHostAddresses(address);
                        if (addresses.Length > 0)
                            return addresses[0];

                        message = $"Could not resolve IP address of '{address}'";
                    }
                    catch (SocketException ex)
                    {
                        message = $"An error occurred trying to resolve '{address}'. {ex.Message}";
                    }
                    catch (ArgumentException ex)
                    {
                        message = $"'{address}' is an invalid IP address. {ex.Message}";
                    }

                    tcpLogSinkLogger?.LogError(message);
                    return null;
                }
            }
        }

        private sealed class TcpLoggerProvider : ILoggerProvider
        {
            private readonly ConcurrentDictionary<string, TcpLogger> loggers = new();
            private readonly TcpLogSink logSink;
            private readonly TcpLoggerConfiguration configuration;
            private readonly IExternalScopeProvider externalScopeProvider = new LoggerExternalScopeProvider();

            public TcpLoggerProvider(TcpLogSink logSink, TcpLoggerConfiguration loggerConfiguration)
            {
                this.configuration = loggerConfiguration;
                this.logSink = logSink;
            }

            public ILogger CreateLogger(string categoryName) =>
                this.loggers.GetOrAdd(categoryName, name => new TcpLogger(this.logSink, this.configuration)
                {
                    ExternalScopeProvider = this.externalScopeProvider
                });

            public void Dispose() => this.loggers.Clear();
        }
    }
}

namespace Logger
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

    public sealed class TcpLogSink : IDisposable
    {
        private readonly ILogger? logger;
        private readonly IPEndPoint serverEndpoint;
        private readonly Func<string, string>? formatter;
        private readonly int maxRetryAttempts;
        private readonly TimeSpan retryDelay;
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly Channel<string> channel;

        public static TcpLogSink Start(IPEndPoint serverEndPoint,
                                       int? maxRetryAttempts = null,
                                       TimeSpan? retryDelay = null,
                                       int? backlogCapacity = null,
                                       Func<string, string>? formatter = null,
                                       ILogger<TcpLogSink>? logger = null)
        {
            var sink = new TcpLogSink(serverEndPoint, maxRetryAttempts, retryDelay, backlogCapacity, formatter, logger);
            _ = Task.Run(() => sink.SendAllLogMessagesAsync(sink.cancellationTokenSource.Token));
            return sink;
        }

        private TcpLogSink(IPEndPoint serverEndpoint,
                           int? maxRetryAttempts, TimeSpan? retryDelay, int? backlogCapacity,
                           Func<string, string>? formatter,
                           ILogger? logger)
        {
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
