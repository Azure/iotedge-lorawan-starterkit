// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;
    using StackExchange.Redis;
    using LoRaTools;

    internal interface ILnsRemoteCallListener
    {
        void Subscribe(string lns, Func<LnsRemoteCall, Task> function);
    }

    internal sealed class RedisRemoteCallListener : ILnsRemoteCallListener
    {
        private readonly ConnectionMultiplexer redis;

        public RedisRemoteCallListener(ConnectionMultiplexer redis)
        {
            this.redis = redis;
        }

        public void Subscribe(string lns, Func<LnsRemoteCall, Task> function)
        {
            this.redis.GetSubscriber().Subscribe(lns).OnMessage(value =>
                function(JsonSerializer.Deserialize<LnsRemoteCall>(value.Message) ?? throw new ArgumentException("Input LnsRemoteCall json was not parsed as valid one.")));
        }
    }
}
