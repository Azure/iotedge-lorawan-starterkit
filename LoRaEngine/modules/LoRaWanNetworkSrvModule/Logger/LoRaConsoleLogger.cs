// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

namespace LoRaWan
{
    using System;
    using System.Collections.Concurrent;
    using Logger;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Configuration;
    using Microsoft.Extensions.Options;

    public class LoRaConsoleLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, LoRaConsoleLogger> loggers = new();

        internal LoggerConfigurationMonitor LoggerConfigurationMonitor { get; }

        public LoRaConsoleLoggerProvider(IOptionsMonitor<LoRaLoggerConfiguration> config)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));

            LoggerConfigurationMonitor = new LoggerConfigurationMonitor(config);
        }

        public LogLevel LogLevel => LoggerConfigurationMonitor.Configuration.LogLevel;
        public EventId EventId => LoggerConfigurationMonitor.Configuration.EventId;

        public ILogger CreateLogger(string categoryName) =>
            this.loggers.GetOrAdd(categoryName, name => new LoRaConsoleLogger(this));

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
                LoggerConfigurationMonitor.Dispose();
            }
        }
    }

    /// <summary>
    /// The LoRaConsoleLogger is a simple console logger that is excluding category and timestamp information.
    /// It's a plain message output with support for structured parameters. Scopes are supported and can
    /// be used to include standard output values in the message.
    /// </summary>
    public class LoRaConsoleLogger : ILogger
    {
        private readonly LoRaConsoleLoggerProvider provider;

        public LoRaConsoleLogger(LoRaConsoleLoggerProvider consoleLoggerProvider)
        {
            _ = consoleLoggerProvider ?? throw new ArgumentNullException(nameof(consoleLoggerProvider));
            this.provider = consoleLoggerProvider;
        }

        public IDisposable BeginScope<TState>(TState state) =>
            this.provider.LoggerConfigurationMonitor.ScopeProvider is { } scopeProvider ? scopeProvider.Push(state) : NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= this.provider.LogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _ = formatter ?? throw new ArgumentNullException(nameof(formatter));

            if (!IsEnabled(logLevel))
            {
                return;
            }

            var configuredEventId = this.provider.EventId;

            if (configuredEventId == 0 || configuredEventId == eventId)
            {
                var formattedMessage = formatter(state, exception);
                formattedMessage = LoggerHelper.AddScopeInformation(this.provider.LoggerConfigurationMonitor.ScopeProvider, formattedMessage);

                if (logLevel == LogLevel.Error)
                {
                    ConsoleWriteError(formattedMessage);
                }
                else
                {
                    ConsoleWrite(formattedMessage);
                }
            }
        }

        protected virtual void ConsoleWriteError(string message) =>
            Console.Error.WriteLine(message);

        protected virtual void ConsoleWrite(string message) =>
            Console.WriteLine(message);
    }

    public static class LoRaConsoleLoggerExtensions
    {
        public static ILoggingBuilder AddLoRaConsoleLogger(this ILoggingBuilder builder)
        {
            _ = builder ?? throw new ArgumentNullException(nameof(builder));

            builder.AddConfiguration();

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, LoRaConsoleLoggerProvider>());

            LoggerProviderOptions.RegisterProviderOptions<LoRaLoggerConfiguration, LoRaConsoleLoggerProvider>(builder.Services);

            return builder;
        }

        public static ILoggingBuilder AddLoRaConsoleLogger(this ILoggingBuilder builder,
                                                           Action<LoRaLoggerConfiguration> configure)
        {
            _ = builder.AddLoRaConsoleLogger();
            _ = builder.Services.Configure(configure);

            return builder;
        }
    }
}
