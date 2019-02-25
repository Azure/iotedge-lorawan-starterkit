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
            var config = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            var iotHubConnectionString = config.GetValue<string>("IoTHubConnectionString");
            if (iotHubConnectionString == null)
            {
                throw new Exception("Missing IoTHubConnectionString in settings");
            }

            var redisConnectionString = config.GetValue<string>("RedisConnectionString");
            if (redisConnectionString == null)
            {
                throw new Exception("Missing RedisConnection string in settings");
            }

            var redis = ConnectionMultiplexer.Connect(redisConnectionString);
            var redisCache = redis.GetDatabase();
            var deviceCacheStore = new LoRaDeviceCacheRedisStore(redisCache);

            builder.Services.AddSingleton(RegistryManager.CreateFromConnectionString(iotHubConnectionString));
            builder.Services.AddSingleton<ILoRaDeviceCacheStore>(deviceCacheStore);
            builder.Services.AddSingleton<ILoRaADRManager>(new LoRaADRServerManager(new LoRaADRRedisStore(redisCache), new LoRaADRStrategyProvider(), deviceCacheStore));

            builder.Services.AddTransient<CreateEdgeDevice>();
            builder.Services.AddTransient<DeviceGetter>();
            builder.Services.AddTransient<FCntCacheCheck>();
            builder.Services.AddTransient<DuplicateMsgCacheCheck>();
            builder.Services.AddTransient<LoRaADRFunction>();
            builder.Services.AddTransient<FunctionBundlerFunction>();
            builder.Services.AddTransient<FunctionBundlerContext>();
        }
    }
}
