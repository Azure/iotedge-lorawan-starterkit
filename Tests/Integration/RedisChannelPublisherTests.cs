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
    using StackExchange.Redis;
    using Moq;

    public class RedisChannelPublisherTests : FunctionTestBase, IClassFixture<RedisFixture>
    {
        private readonly IChannelPublisher channelPublisher;
        private readonly ITestOutputHelper testOutputHelper;
        private readonly ConnectionMultiplexer redis;

        public RedisChannelPublisherTests(RedisFixture redis, ITestOutputHelper testOutputHelper)
        {
            if (redis is null) throw new ArgumentNullException(nameof(redis));
            this.channelPublisher = new RedisChannelPublisher(redis.Redis, NullLogger<RedisChannelPublisher>.Instance);
            this.testOutputHelper = testOutputHelper;
            this.redis = redis.Redis;
        }

        [Fact]
        public async Task Publish_Aysnc()
        {
            // arrange
            var message = "test message";
            var channel = "channel1";
            var assert = new Mock<Action<ChannelMessage>>();
            this.testOutputHelper.WriteLine("Publishing message...");
            (await this.redis.GetSubscriber().SubscribeAsync(channel)).OnMessage(assert.Object);

            // act
            await this.channelPublisher.PublishAsync(channel, message);

            // assert
            assert.Verify(a => a.Invoke(It.Is<ChannelMessage>(actual => actual.Message == message)), Times.Once);
        }
    }
}
