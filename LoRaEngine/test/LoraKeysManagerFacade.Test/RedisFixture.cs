// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using StackExchange.Redis;

    /// <summary>
    /// Test fixture for tests using Redis
    /// </summary>
    public class RedisFixture : IDisposable
    {
        private ConnectionMultiplexer redis;

        public IDatabase Database { get; }

        public RedisFixture()
        {
            var redisConnectionString = "localhost:6379";
            try
            {
                this.redis = ConnectionMultiplexer.Connect(redisConnectionString);
                this.Database = this.redis.GetDatabase();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to connect to redis at '{redisConnectionString}'. If running locally with docker: run 'docker run -d -p 6379:6379 redis'. If running in Azure DevOps: run redis in docker.", ex);
            }
        }

        public void Dispose()
        {
            this.redis?.Dispose();
            this.redis = null;
        }
    }
}
