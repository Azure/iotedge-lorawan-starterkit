// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Logger
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaWan;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Configuration;
    using Microsoft.Extensions.Options;

    public class IotHubLoggerConfiguration
    {
        public LogLevel LogLevel { get; set; }
        public EventId EventId { get; set; }
        public bool UseScopes { get; set; } = true;
    }

#pragma warning disable CA1812 // Class never instantiated
    internal sealed class IotHubLoggerProvider : ILoggerProvider
#pragma warning restore CA1812 // Class never instantiated
    {
        private readonly ConcurrentDictionary<string, IotHubLogger> loggers = new();
        private readonly IDisposable onChangeToken;
        private readonly Lazy<Task<ModuleClient>> moduleClientFactory;

        public IotHubLoggerConfiguration Configuration { get; private set; }
        public LoggerExternalScopeProvider? ScopeProvider { get; private set; }

        public IotHubLoggerProvider(IOptionsMonitor<IotHubLoggerConfiguration> optionsMonitor)
        {
            this.onChangeToken = optionsMonitor.OnChange(UpdateConfiguration);
            UpdateConfiguration(optionsMonitor.CurrentValue);
            this.moduleClientFactory = new Lazy<Task<ModuleClient>>(ModuleClient.CreateFromEnvironmentAsync(new[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) }));
        }

        [MemberNotNull(nameof(Configuration))]
        private void UpdateConfiguration(IotHubLoggerConfiguration configuration)
        {
            Configuration = configuration;
            ScopeProvider = Configuration.UseScopes ? new LoggerExternalScopeProvider() : null;
        }

        public ILogger CreateLogger(string categoryName) =>
            this.loggers.GetOrAdd(categoryName, n => new IotHubLogger(this, this.moduleClientFactory));

        public void Dispose()
        {
            this.onChangeToken?.Dispose();
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
                        using var m = new Message(Encoding.UTF8.GetBytes(formattedMessage));
                        var mc = await this.moduleClientFactory.Value;
                        await mc.SendEventAsync(m);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error when sending log to IoT Hub: {ex}");
                        throw;
                    }
                });
            }
        }
    }

    public static class IotHubLoggerExtensions
    {
        public static ILoggingBuilder AddIotHubLogger(this ILoggingBuilder builder, Action<IotHubLoggerConfiguration> configure)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, IotHubLoggerProvider>());
            LoggerProviderOptions.RegisterProviderOptions<IotHubLoggerConfiguration, IotHubLoggerProvider>(builder.Services);
            _ = builder.Services.Configure(configure);
            return builder;
        }
    }
}
