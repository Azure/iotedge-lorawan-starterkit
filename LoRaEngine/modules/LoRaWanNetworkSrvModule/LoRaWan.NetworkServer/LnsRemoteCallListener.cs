// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;
    using StackExchange.Redis;

    internal interface ILnsRemoteCallListener
    {
        void Subscribe(string lns, Func<string, Task> function);
    }

    internal sealed class RedisRemoteCallListener : ILnsRemoteCallListener
    {
        private readonly ConnectionMultiplexer redis;

        public RedisRemoteCallListener(ConnectionMultiplexer redis)
        {
            this.redis = redis;
        }

        public void Subscribe(string lns, Func<string, Task> function)
        {
            this.redis.GetSubscriber().Subscribe(lns).OnMessage(value => function(value.Message));
        }
    }
}
