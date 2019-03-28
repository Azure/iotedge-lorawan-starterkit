// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[assembly: Microsoft.Azure.WebJobs.Hosting.WebJobsStartup(typeof(LoraKeysManagerFacade.FacadeStartup))]

namespace LoraKeysManagerFacade
{
    using System;
    using LoraKeysManagerFacade.FunctionBundler;
    using LoRaTools.ADR;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using StackExchange.Redis;

    public class FacadeStartup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            var configHandler = ConfigHandler.Create(builder);

            var iotHubConnectionString = configHandler.IoTHubConnectionString;
            if (iotHubConnectionString == null)
            {
                throw new Exception($"Missing {ConfigHandler.IoTHubConnectionStringKey} in settings");
            }

            var redisConnectionString = configHandler.RedisConnectionString;
            if (redisConnectionString == null)
            {
                throw new Exception($"Missing {ConfigHandler.RedisConnectionStringKey} in settings");
            }

            var redis = ConnectionMultiplexer.Connect(redisConnectionString);
            var redisCache = redis.GetDatabase();
            var deviceCacheStore = new LoRaDeviceCacheRedisStore(redisCache);

            builder.Services.AddSingleton(RegistryManager.CreateFromConnectionString(iotHubConnectionString));
            builder.Services.AddSingleton<IServiceClient>(new ServiceClientAdapter(ServiceClient.CreateFromConnectionString(iotHubConnectionString)));
            builder.Services.AddSingleton<ILoRaDeviceCacheStore>(deviceCacheStore);
            builder.Services.AddSingleton<ILoRaADRManager>(new LoRaADRServerManager(new LoRaADRRedisStore(redisCache), new LoRaADRStrategyProvider(), deviceCacheStore));
            builder.Services.AddSingleton<CreateEdgeDevice>();
            builder.Services.AddSingleton<DeviceGetter>();
            builder.Services.AddSingleton<FCntCacheCheck>();
            builder.Services.AddSingleton<FunctionBundlerFunction>();
            builder.Services.AddSingleton<IFunctionBundlerExecutionItem, NextFCntDownExecutionItem>();
            builder.Services.AddSingleton<IFunctionBundlerExecutionItem, DeduplicationExecutionItem>();
            builder.Services.AddSingleton<IFunctionBundlerExecutionItem, ADRExecutionItem>();
            builder.Services.AddSingleton<IFunctionBundlerExecutionItem, PreferredGatewayExecutionItem>();
            builder.Services.AddSingleton<IFunctionBundlerExecutionItem, ResetDeviceCacheExecutionItem>();
        }

        abstract class ConfigHandler
        {
            internal const string IoTHubConnectionStringKey = "IoTHubConnectionString";
            internal const string RedisConnectionStringKey = "RedisConnectionString";

            internal static ConfigHandler Create(IWebJobsBuilder builder)
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

            class ProductionConfigHandler : ConfigHandler
            {
                private readonly IConfiguration config;

                internal ProductionConfigHandler(IConfiguration config)
                {
                    this.config = config;
                }

                internal override string RedisConnectionString => this.config.GetConnectionString(RedisConnectionStringKey);

                internal override string IoTHubConnectionString => this.config.GetConnectionString(IoTHubConnectionStringKey);
            }

            class LocalConfigHandler : ConfigHandler
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
