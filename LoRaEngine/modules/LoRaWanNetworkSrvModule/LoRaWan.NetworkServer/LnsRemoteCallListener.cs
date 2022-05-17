// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using StackExchange.Redis;
    using LoRaTools;

    internal interface ILnsRemoteCallListener
    {
        Task SubscribeAsync(string lns, Func<LnsRemoteCall, Task> function, CancellationToken cancellationToken);

        Task UnsubscribeAsync(string lns, CancellationToken cancellationToken);
    }

    internal sealed class RedisRemoteCallListener : ILnsRemoteCallListener
    {
        private readonly ConnectionMultiplexer redis;

        public RedisRemoteCallListener(ConnectionMultiplexer redis)
        {
            this.redis = redis;
        }

        // Cancellation token to be passed when/if a future update to SubscribeAsync is allowing to use it
        public async Task SubscribeAsync(string lns, Func<LnsRemoteCall, Task> function, CancellationToken cancellationToken)
        {
            var channelMessage = await this.redis.GetSubscriber().SubscribeAsync(lns);
            channelMessage.OnMessage(value =>
            {
                var lnsRemoteCall = JsonSerializer.Deserialize<LnsRemoteCall>(value.Message) ?? throw new InvalidOperationException("Deserialization produced an empty LnsRemoteCall.");
                return function(lnsRemoteCall);
            });
        }

        // Cancellation token to be passed when/if a future update to UnsubscribeAsync is allowing to use it
        public async Task UnsubscribeAsync(string lns, CancellationToken cancellationToken)
        {
            await this.redis.GetSubscriber().UnsubscribeAsync(lns);
        }
    }
}
