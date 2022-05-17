// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using StackExchange.Redis;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using LoRaTools;
    using System.Text.Json;

    public class RedisChannelPublisher : IChannelPublisher
    {
        private readonly ConnectionMultiplexer redis;
        private readonly ISubscriber subscriber;
        private readonly ILogger logger;

        public RedisChannelPublisher(ConnectionMultiplexer redis, ILogger<RedisChannelPublisher> logger)
        {
            this.redis = redis;
            this.logger = logger;
            this.subscriber = this.redis.GetSubscriber();
        }

        public async Task PublishAsync(string channel, LnsRemoteCall lnsRemoteCall)
        {
            this.logger.LogDebug("Publishing message to channel '{Channel}'.", channel);
            _ = await this.subscriber.PublishAsync(channel, JsonSerializer.Serialize(lnsRemoteCall));
        }
    }
}
