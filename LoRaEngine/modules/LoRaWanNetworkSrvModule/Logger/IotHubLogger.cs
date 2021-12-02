// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("LoRaWan.Tests.Unit")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace Logger
{
    using System;
    using System.Collections.Concurrent;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaWan;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;

    public record IotHubLoggerConfiguration(LogLevel LogLevel, EventId EventId, bool UseScopes);

    internal sealed class IotHubLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, IotHubLogger> loggers = new();
        private readonly Lazy<Task<ModuleClient>> moduleClientFactory;

        public IotHubLoggerConfiguration Configuration { get; }
        public LoggerExternalScopeProvider? ScopeProvider { get; }

        public IotHubLoggerProvider(IotHubLoggerConfiguration configuration)
            : this(configuration, new Lazy<Task<ModuleClient>>(ModuleClient.CreateFromEnvironmentAsync(new[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) })))
        { }

        internal IotHubLoggerProvider(IotHubLoggerConfiguration configuration, Lazy<Task<ModuleClient>> moduleClientFactory)
        {
            Configuration = configuration;
            ScopeProvider = configuration.UseScopes ? new LoggerExternalScopeProvider() : null;
            this.moduleClientFactory = moduleClientFactory;
        }

        public ILogger CreateLogger(string categoryName) =>
            this.loggers.GetOrAdd(categoryName, n => new IotHubLogger(this, this.moduleClientFactory));

        public void Dispose()
        {
            this.loggers.Clear();
        }
    }

    internal class IotHubLogger : ILogger
    {
        private readonly IotHubLoggerProvider iotHubLoggerProvider;
        private readonly Lazy<Task<ModuleClient>> moduleClientFactory;

        public IotHubLogger(IotHubLoggerProvider iotHubLoggerProvider,
                            Lazy<Task<ModuleClient>> moduleClientFactory)
        {
            this.iotHubLoggerProvider = iotHubLoggerProvider;
            this.moduleClientFactory = moduleClientFactory;
        }

        public IDisposable BeginScope<TState>(TState state) =>
            this.iotHubLoggerProvider.ScopeProvider?.Push(state) ?? NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= this.iotHubLoggerProvider.Configuration.LogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _ = formatter ?? throw new ArgumentNullException(nameof(formatter));

            if (!IsEnabled(logLevel))
                return;

            var configuredEventId = this.iotHubLoggerProvider.Configuration.EventId;
            if (configuredEventId == 0 || configuredEventId == eventId)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var formattedMessage = LoggerHelper.AddScopeInformation(this.iotHubLoggerProvider.ScopeProvider, formatter(state, exception));
                        await SendAsync(formattedMessage);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error when sending log to IoT Hub: {ex}");
                        throw;
                    }
                });
            }
        }

        internal virtual async Task SendAsync(string message)
        {
            using var m = new Message(Encoding.UTF8.GetBytes(message));
            var moduleClient = await this.moduleClientFactory.Value;
            await moduleClient.SendEventAsync(m);
        }
    }

    public static class IotHubLoggerExtensions
    {
        public static ILoggingBuilder AddIotHubLogger(this ILoggingBuilder builder, IotHubLoggerConfiguration configuration)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, IotHubLoggerProvider>(_ => new IotHubLoggerProvider(configuration)));
            return builder;
        }
    }
}
