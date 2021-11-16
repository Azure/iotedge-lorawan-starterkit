// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[assembly: Microsoft.Azure.Functions.Extensions.DependencyInjection.FunctionsStartup(typeof(LoraKeysManagerFacade.FacadeStartup))]

namespace LoraKeysManagerFacade
{
    using System;
    using LoraKeysManagerFacade.FunctionBundler;
    using LoRaTools.ADR;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Functions.Extensions.DependencyInjection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using StackExchange.Redis;

    public class FacadeStartup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));

            var configHandler = ConfigHandler.Create(builder);

            var iotHubConnectionString = configHandler.IoTHubConnectionString;
            if (iotHubConnectionString == null)
            {
                throw new InvalidOperationException($"Missing {ConfigHandler.IoTHubConnectionStringKey} in settings");
            }

            var redisConnectionString = configHandler.RedisConnectionString;
            if (redisConnectionString == null)
            {
                throw new InvalidOperationException($"Missing {ConfigHandler.RedisConnectionStringKey} in settings");
            }

            var redis = ConnectionMultiplexer.Connect(redisConnectionString);
            var redisCache = redis.GetDatabase();
            var deviceCacheStore = new LoRaDeviceCacheRedisStore(redisCache);

#pragma warning disable CA2000 // Dispose objects before losing scope
            // Object is handled by DI container.
            _ = builder.Services.AddSingleton(RegistryManager.CreateFromConnectionString(iotHubConnectionString));
#pragma warning restore CA2000 // Dispose objects before losing scope
            _ = builder.Services
                    .AddSingleton<IServiceClient>(new ServiceClientAdapter(ServiceClient.CreateFromConnectionString(iotHubConnectionString)))
                    .AddSingleton<ILoRaDeviceCacheStore>(deviceCacheStore)
                    .AddSingleton<ILoRaADRManager>(sp => new LoRaADRServerManager(new LoRaADRRedisStore(redisCache, sp.GetService<ILogger<LoRaADRRedisStore>>()), new LoRaADRStrategyProvider(), deviceCacheStore))
                    .AddSingleton<CreateEdgeDevice>()
                    .AddSingleton<DeviceGetter>()
                    .AddSingleton<FCntCacheCheck>()
                    .AddSingleton<FunctionBundlerFunction>()
                    .AddSingleton<IFunctionBundlerExecutionItem, NextFCntDownExecutionItem>()
                    .AddSingleton<IFunctionBundlerExecutionItem, DeduplicationExecutionItem>()
                    .AddSingleton<IFunctionBundlerExecutionItem, ADRExecutionItem>()
                    .AddSingleton<IFunctionBundlerExecutionItem, PreferredGatewayExecutionItem>()
                    .AddSingleton<LoRaDevAddrCache>();
        }

        private abstract class ConfigHandler
        {
            internal const string IoTHubConnectionStringKey = "IoTHubConnectionString";
            internal const string RedisConnectionStringKey = "RedisConnectionString";

            internal static ConfigHandler Create(IFunctionsHostBuilder builder)
            {
                var tempProvider = builder.Services.BuildServiceProvider();
                var config = tempProvider.GetRequiredService<IConfiguration>();

                var iotHubConnectionString = config.GetConnectionString(IoTHubConnectionStringKey);
                if (!string.IsNullOrEmpty(iotHubConnectionString))
                {
                    return new ProductionConfigHandler(config);
                }

                return new LocalConfigHandler();
            }

            internal abstract string RedisConnectionString { get; }

            internal abstract string IoTHubConnectionString { get; }

            private class ProductionConfigHandler : ConfigHandler
            {
                private readonly IConfiguration config;

                internal ProductionConfigHandler(IConfiguration config)
                {
                    this.config = config;
                }

                internal override string RedisConnectionString => this.config.GetConnectionString(RedisConnectionStringKey);

                internal override string IoTHubConnectionString => this.config.GetConnectionString(IoTHubConnectionStringKey);
            }

            private class LocalConfigHandler : ConfigHandler
            {
                private readonly IConfiguration config;

                internal LocalConfigHandler()
                {
                    this.config = new ConfigurationBuilder()
                                    .SetBasePath(Environment.CurrentDirectory)
                                    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: false)
                                    .AddEnvironmentVariables()
                                    .Build();
                }

                internal override string RedisConnectionString => this.config.GetValue<string>(RedisConnectionStringKey);

                internal override string IoTHubConnectionString => this.config.GetValue<string>(IoTHubConnectionStringKey);
            }
        }
    }
}
