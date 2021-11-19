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

    internal class TcpLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, TcpLogger> loggers = new();
        private readonly ILogSink logSink;
        private readonly TcpLoggerConfiguration configuration;
        private readonly IExternalScopeProvider externalScopeProvider = new LoggerExternalScopeProvider();

        public TcpLoggerProvider(ILogSink logSink, TcpLoggerConfiguration loggerConfiguration)
        {
            this.configuration = loggerConfiguration;
            this.logSink = logSink;
        }

        public ILogger CreateLogger(string categoryName) =>
            this.loggers.GetOrAdd(categoryName, name => new TcpLogger(this.logSink, this.configuration)
            {
                ExternalScopeProvider = this.externalScopeProvider
            });

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.loggers.Clear();
            }
        }
    }

    internal class TcpLogger : ILogger
    {
        private readonly ILogSink logSink;
        private readonly TcpLoggerConfiguration loggerConfiguration;

        public TcpLogger(ILogSink logSink,
                         TcpLoggerConfiguration loggerConfiguration)
        {
            this.logSink = logSink;
            this.loggerConfiguration = loggerConfiguration ?? throw new ArgumentNullException(nameof(loggerConfiguration));
        }

        /// <summary>
        /// Gets or sets the external scope provider.
        /// </summary>
        internal IExternalScopeProvider? ExternalScopeProvider { get; set; }

        public IDisposable? BeginScope<TState>(TState state) =>
            ExternalScopeProvider is { } scopeProvider ? scopeProvider.Push(state) : default;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= this.loggerConfiguration.LogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
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
        public static ILoggingBuilder AddTcpLogger(this ILoggingBuilder builder, TcpLoggerConfiguration configuration)
        {
            _ = builder ?? throw new ArgumentNullException(nameof(builder));

            builder.AddConfiguration();
            _ = builder.Services.AddSingleton(_ => Init(configuration));
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, TcpLoggerProvider>(sp => new TcpLoggerProvider(sp.GetRequiredService<ILogSink>(), configuration)));

            return builder;

            static ILogSink Init(TcpLoggerConfiguration configuration, ILogger<TcpLogSink>? tcpLogSinkLogger = null)
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
                    ? TcpLogSink.Start(someEndPoint, configuration.LogLevel,
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
    }
}
