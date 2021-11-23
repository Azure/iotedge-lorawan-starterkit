// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Logger
{
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
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
    using LoRaWan;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Configuration;
    using Microsoft.Extensions.Primitives;

    /// <summary>
    /// TcpLogger logs to a TCP endpoint that is listening on this endpoint. This is used only for E2E tests.
    /// TcpLogger does not support category names or event IDs.
    /// </summary>
    internal sealed class TcpLogger : ILogger
    {
        private readonly LogLevel logLevel;
        private readonly IExternalScopeProvider externalScopeProvider;
        private readonly Action<string> logger;

        public TcpLogger(LogLevel logLevel, IExternalScopeProvider externalScopeProvider, Action<string> logger)
        {
            this.externalScopeProvider = externalScopeProvider;
            this.logLevel = logLevel;
            this.logger = logger;
        }

        public IDisposable BeginScope<TState>(TState state) =>
            this.externalScopeProvider is { } scopeProvider ? scopeProvider.Push(state) : NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= this.logLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _ = formatter ?? throw new ArgumentNullException(nameof(formatter));

            if (!IsEnabled(logLevel))
                return;

            var formattedMessage = LoggerHelper.AddScopeInformation(this.externalScopeProvider, formatter(state, exception), ":");

            this.logger(formattedMessage);
        }
    }

    public static class TcpLoggerExtensions
    {
        public static ILoggingBuilder AddTcpLogger(this ILoggingBuilder builder, TcpLoggerConfiguration configuration, ILoggerFactory? loggerFactory = null)
        {
            _ = builder ?? throw new ArgumentNullException(nameof(builder));

            builder.AddConfiguration();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, TcpLoggerProvider>(_ =>
                TcpLoggerProvider.Start(configuration, loggerFactory?.CreateLogger<TcpLoggerProvider>())));
            return builder;
        }

        private sealed class TcpLoggerProvider : ILoggerProvider
        {
            private readonly ConcurrentDictionary<string, TcpLogger> loggers = new();
            private readonly IExternalScopeProvider externalScopeProvider = new LoggerExternalScopeProvider();
            private readonly LogLevel logLevel;
            private readonly ILogger? logger;
            private readonly IPEndPoint serverEndpoint;
            private readonly Func<string, string>? formatter;
            private readonly int maxRetryAttempts;
            private readonly TimeSpan retryDelay;
            private readonly CancellationTokenSource cancellationTokenSource = new();
            private readonly Channel<string> channel;

            public static TcpLoggerProvider Start(TcpLoggerConfiguration configuration, ILogger<TcpLoggerProvider>? logger = null)
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

                if (endPoint is null)
                    throw new InvalidOperationException($"TCP endpoint is incorrectly configured. Address is '{configuration.LogToTcpAddress}' and port is '{configuration.LogToTcpPort}'");

                var provider = new TcpLoggerProvider(configuration.LogLevel, endPoint, logger: logger,
                                                     formatter: string.IsNullOrEmpty(configuration.GatewayId)
                                                                ? null
                                                                : msg => $"[{configuration.GatewayId}] {msg}");

                _ = Task.Run(() => provider.SendAllLogMessagesAsync(provider.cancellationTokenSource.Token));

                return provider;

                IPAddress? Resolve(string address)
                {
                    if (IPAddress.TryParse(address, out var ipAddress))
                        return ipAddress;

                    try
                    {
                        // try to parse the address as dns
                        var addresses = Dns.GetHostAddresses(address);
                        if (addresses.Length > 0)
                            return addresses[0];

                        logger?.LogError("Could not resolve IP address of '{Address}'", address);
                    }
                    catch (SocketException ex)
                    {
                        logger?.LogError(ex, "An error occurred trying to resolve '{Address}'.", address);
                    }
                    catch (ArgumentException ex)
                    {
                        logger?.LogError(ex, "'{Address}' is an invalid IP address.", address);
                    }

                    return null;
                }
            }

            private TcpLoggerProvider(LogLevel logLevel, IPEndPoint serverEndpoint,
                                      int? maxRetryAttempts = null, TimeSpan? retryDelay = null, int? backlogCapacity = null,
                                      Func<string, string>? formatter = null,
                                      ILogger? logger = null)
            {
                this.logLevel = logLevel;
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

                this.loggers.Clear();
            }

            public ILogger CreateLogger(string categoryName) =>
                this.loggers.GetOrAdd(categoryName, _ => new TcpLogger(this.logLevel, this.externalScopeProvider, Log));

            private void Log(string message)
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
                    throw new ObjectDisposedException(nameof(TcpLoggerProvider));

                _ = this.channel.Writer.TryWrite(this.formatter?.Invoke(message) ?? message);
            }

            private static readonly char[] NewLineChars = { '\n', '\r' };

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
                                        await client.ConnectAsync(this.serverEndpoint.Address, this.serverEndpoint.Port, cancellationToken);
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
        }
    }
}
