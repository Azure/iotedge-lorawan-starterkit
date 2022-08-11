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
    using LoRaTools;
    using System.Text.Json;

    [Collection(RedisFixture.CollectionName)]
#pragma warning disable xUnit1033 // False positive: Test classes decorated with 'Xunit.IClassFixture<TFixture>' or 'Xunit.ICollectionFixture<TFixture>' should add a constructor argument of type TFixture
    public class RedisChannelPublisherTests : IClassFixture<RedisFixture>
#pragma warning restore xUnit1033 // False positive: Test classes decorated with 'Xunit.IClassFixture<TFixture>' or 'Xunit.ICollectionFixture<TFixture>' should add a constructor argument of type TFixture
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
            var message = new LnsRemoteCall(RemoteCallKind.CloseConnection, "test message");
            var serializedMessage = JsonSerializer.Serialize(message);
            var channel = "channel1";
            var assert = new Mock<Action<ChannelMessage>>();
            this.testOutputHelper.WriteLine("Publishing message...");
            (await this.redis.GetSubscriber().SubscribeAsync(channel)).OnMessage(assert.Object);

            // act
            await this.channelPublisher.PublishAsync(channel, message);

            // assert
            await assert.RetryVerifyAsync(a => a.Invoke(It.Is<ChannelMessage>(actual => actual.Message == serializedMessage)), Times.Once);
        }
    }
}
