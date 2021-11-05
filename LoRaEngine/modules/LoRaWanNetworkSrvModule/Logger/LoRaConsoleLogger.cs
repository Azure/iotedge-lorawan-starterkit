// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

namespace LoRaWan
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Configuration;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    public class LoRaConsoleLoggerConfiguration
    {
        public LogLevel LogLevel { get; set; }
        public EventId EventId { get; set; }
        public bool UseScopes { get; set; } = true;
    }

    public class LoRaConsoleLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, LoRaConsoleLogger> loggers = new();
        private readonly IDisposable onChangeToken;

        public LoRaConsoleLoggerProvider(IOptionsMonitor<LoRaConsoleLoggerConfiguration> config)
        {
            _ = config ?? throw new ArgumentNullException(nameof(config));

            this.onChangeToken = config.OnChange(UpdateConfiguration);
            UpdateConfiguration(config.CurrentValue);
        }

        [MemberNotNull(nameof(Configuration))]
        private void UpdateConfiguration(LoRaConsoleLoggerConfiguration configuration)
        {
            Configuration = configuration;
            ScopeProvider = Configuration.UseScopes ? new LoggerExternalScopeProvider() : null;
        }

        public LogLevel LogLevel => Configuration.LogLevel;
        public EventId EventId => Configuration.EventId;
        internal LoRaConsoleLoggerConfiguration Configuration { get; private set; }
        internal IExternalScopeProvider? ScopeProvider { get; private set; }

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
                this.onChangeToken.Dispose();
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

        public IDisposable? BeginScope<TState>(TState state) =>
            this.provider.ScopeProvider is { } scopeProvider ? scopeProvider.Push(state) : default;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= this.provider.LogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
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
                formattedMessage = AddScopeInformation(formattedMessage);

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

        /// <summary>
        /// Prefixes the message with predefined scope values.
        /// Right now we only support DevEUI, but could be extended to
        /// include others (StationEUI, Gateway, DevAddr etc).
        /// If the message is already prefixed with the DevEUI, we don't
        /// add it again.
        /// </summary>
        /// <param name="message">The already formatted message</param>
        /// <returns></returns>
        private string AddScopeInformation(string message)
        {
            if (this.provider.ScopeProvider is { } scopeProvider)
            {
                scopeProvider.ForEachScope<object?>((activeScope, _) =>
                {
                    if (activeScope is IDictionary<string, object> activeScopeDictionary &&
                        activeScopeDictionary.TryGetValue(ILoggerExtensions.DevEUIKey, out var obj) &&
                        obj is string devEUI &&
                        !message.StartsWith(devEUI, StringComparison.OrdinalIgnoreCase))
                    {
                        message = string.Concat(devEUI, " ", message);
                    }
                }, null);
            }

            return message;
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

            LoggerProviderOptions.RegisterProviderOptions<LoRaConsoleLoggerConfiguration, LoRaConsoleLoggerProvider>(builder.Services);

            return builder;
        }

        public static ILoggingBuilder AddLoRaConsoleLogger(this ILoggingBuilder builder,
                                                           Action<LoRaConsoleLoggerConfiguration> configure)
        {
            _ = builder.AddLoRaConsoleLogger();
            _ = builder.Services.Configure(configure);

            return builder;
        }
    }
}
