// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Configuration;
    using StackExchange.Redis;

    public class LoRaDeviceCacheRedisStore : ILoRaDeviceCacheStore
    {
        private IDatabase redisCache;

        public LoRaDeviceCacheRedisStore(ExecutionContext context)
        {
            var config = new ConfigurationBuilder()
                                .SetBasePath(context.FunctionAppDirectory)
                                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                                .AddEnvironmentVariables()
                                .Build();

            var redisConnectionString = config.GetConnectionString("RedisConnectionString");

            if (string.IsNullOrEmpty(redisConnectionString))
            {
                string errorMsg = "Missing RedisConnectionString in settings";
                throw new Exception(errorMsg);
            }

            var redis = ConnectionMultiplexer.Connect(redisConnectionString);
            this.redisCache = redis.GetDatabase();
        }

        public bool LockTake(string key, string value, TimeSpan timeout)
        {
            return this.redisCache.LockTake(key, value, timeout);
        }

        public string StringGet(string key)
        {
            return this.redisCache.StringGet(key, CommandFlags.DemandMaster);
        }

        public bool StringSet(string key, string value, TimeSpan? expiry)
        {
            return this.redisCache.StringSet(key, value, expiry, When.Always, CommandFlags.DemandMaster);
        }

        public bool KeyDelete(string key)
        {
            return this.redisCache.KeyDelete(key);
        }

        public bool LockRelease(string key, string value)
        {
            return this.redisCache.LockRelease(key, value);
        }
    }
}
