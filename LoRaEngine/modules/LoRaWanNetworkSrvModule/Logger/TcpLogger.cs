// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Logger
{
    using System;
    using System.Collections.Concurrent;
    using LoRaWan;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Configuration;

    internal class TcpLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, TcpLogger> loggers = new();
        private readonly ILogSink logSink;
        private readonly LoggerConfiguration configuration;
        private readonly IExternalScopeProvider externalScopeProvider = new LoggerExternalScopeProvider();

        public TcpLoggerProvider(ILogSink logSink, LoggerConfiguration loggerConfiguration)
        {
            this.configuration = loggerConfiguration;
            this.logSink = logSink;
        }

        public ILogger CreateLogger(string categoryName) =>
            this.loggers.GetOrAdd(categoryName, name => new TcpLogger(categoryName, this.logSink, this.configuration)
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
        private readonly string categoryName;
        private readonly ILogSink logSink;
        private readonly LoggerConfiguration loggerConfiguration;

        public TcpLogger(string categoryName,
                         ILogSink logSink,
                         LoggerConfiguration loggerConfiguration)
        {
            this.categoryName = categoryName;
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

            var formattedMessage = LoggerHelper.AddScopeInformation(ExternalScopeProvider, formatter(state, exception));

            this.logSink.Log(logLevel, formattedMessage);
        }
    }

    public static class LoRaConsoleLoggerExtensions
    {
        public static ILoggingBuilder AddTcpLogger(this ILoggingBuilder builder, LoggerConfiguration configuration)
        {
            _ = builder ?? throw new ArgumentNullException(nameof(builder));

            builder.AddConfiguration();
            _ = builder.Services.AddSingleton(_ => LoRaWan.TcpLogger.Init(configuration));
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, TcpLoggerProvider>(sp => new TcpLoggerProvider(sp.GetRequiredService<ILogSink>(), configuration)));

            return builder;
        }
    }
}
