// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

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
    using Microsoft.Extensions.Logging.Configuration;
    using Microsoft.Extensions.Options;

    internal sealed class IotHubLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, IotHubLogger> loggers = new();
        private readonly Lazy<Task<ModuleClient>> moduleClientFactory;

        internal LoggerConfigurationMonitor LoggerConfigurationMonitor { get; }

        public IotHubLoggerProvider(IOptionsMonitor<LoRaLoggerConfiguration> configuration)
            : this(configuration, new Lazy<Task<ModuleClient>>(ModuleClient.CreateFromEnvironmentAsync(new[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) })))
        { }

        internal IotHubLoggerProvider(IOptionsMonitor<LoRaLoggerConfiguration> configuration, Lazy<Task<ModuleClient>> moduleClientFactory)
        {
            LoggerConfigurationMonitor = new LoggerConfigurationMonitor(configuration);
            this.moduleClientFactory = moduleClientFactory;
        }

        public ILogger CreateLogger(string categoryName) =>
            this.loggers.GetOrAdd(categoryName, n => new IotHubLogger(this, this.moduleClientFactory));

        public void Dispose()
        {
            this.loggers.Clear();
            this.LoggerConfigurationMonitor.Dispose();
        }
    }

    internal class IotHubLogger : ILogger
    {
        private readonly IotHubLoggerProvider iotHubLoggerProvider;
        private readonly Lazy<Task<ModuleClient>> moduleClientFactory;

        internal bool hasError;

        public IotHubLogger(IotHubLoggerProvider iotHubLoggerProvider,
                            Lazy<Task<ModuleClient>> moduleClientFactory)
        {
            this.iotHubLoggerProvider = iotHubLoggerProvider;
            this.moduleClientFactory = moduleClientFactory;
        }

        public IDisposable BeginScope<TState>(TState state) =>
            this.iotHubLoggerProvider.LoggerConfigurationMonitor.ScopeProvider?.Push(state) ?? NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) =>
            !this.hasError && logLevel >= this.iotHubLoggerProvider.LoggerConfigurationMonitor.Configuration.LogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _ = formatter ?? throw new ArgumentNullException(nameof(formatter));

            if (!IsEnabled(logLevel))
                return;

            var configuredEventId = this.iotHubLoggerProvider.LoggerConfigurationMonitor.Configuration.EventId;
            if (configuredEventId == 0 || configuredEventId == eventId)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var formattedMessage = LoggerHelper.AddScopeInformation(this.iotHubLoggerProvider.LoggerConfigurationMonitor.ScopeProvider, formatter(state, exception));
                        ModuleClient moduleClient;
                        try
                        {
                            moduleClient = await this.moduleClientFactory.Value;
                        }
                        catch (Exception)
                        {
                            this.hasError = true;
                            throw;
                        }

                        await SendAsync(moduleClient, formattedMessage);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error when sending log to IoT Hub: {ex}");
                        throw;
                    }
                });
            }
        }

        internal virtual async Task SendAsync(ModuleClient moduleClient, string message)
        {
            using var m = new Message(Encoding.UTF8.GetBytes(message));
            await moduleClient.SendEventAsync(m);
        }
    }

    public static class IotHubLoggerExtensions
    {
        public static ILoggingBuilder AddIotHubLogger(this ILoggingBuilder builder, Action<LoRaLoggerConfiguration> configure)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, IotHubLoggerProvider>());
            LoggerProviderOptions.RegisterProviderOptions<LoRaLoggerConfiguration, IotHubLoggerProvider>(builder.Services);
            _ = builder.Services.Configure(configure);
            return builder;
        }
    }
}
