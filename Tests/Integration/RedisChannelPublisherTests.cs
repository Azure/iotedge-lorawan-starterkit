// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan.Tests.Common;
    using Xunit;
    using LoraKeysManagerFacade;
    using Xunit.Abstractions;
    using Microsoft.Extensions.Logging.Abstractions;

    public class RedisChannelPublisherTests : FunctionTestBase, IClassFixture<RedisFixture>
    {
        private readonly IChannelPublisher channelPublisher;
        private readonly ITestOutputHelper testOutputHelper;

        public RedisChannelPublisherTests(RedisFixture redis, ITestOutputHelper testOutputHelper)
        {
            if (redis is null) throw new ArgumentNullException(nameof(redis));
            this.channelPublisher = new RedisChannelPublisher(redis.Redis, NullLogger<RedisChannelPublisher>.Instance);
            this.testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task Publish_Aysnc()
        {
            var message = "test message";
            var channel = "channel1";
            Console.WriteLine("Publishing message...");
            await this.channelPublisher.PublishAsync(channel, message);
        }
    }
}
