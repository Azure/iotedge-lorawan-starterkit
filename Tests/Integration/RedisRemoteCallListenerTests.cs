// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using Moq;
    using StackExchange.Redis;
    using Xunit;

    [Collection(RedisFixture.CollectionName)]
    public sealed class RedisRemoteCallListenerTests : IClassFixture<RedisFixture>
    {
        private readonly ConnectionMultiplexer redis;
        private readonly RedisRemoteCallListener subject;

        public RedisRemoteCallListenerTests(RedisFixture redisFixture)
        {
            this.redis = redisFixture.Redis;
            this.subject = new RedisRemoteCallListener(this.redis);
        }

        [Fact]
        public async Task Subscribe_Rceives_Message()
        {
            // arrange
            var lnsName = "some-lns";
            var message = "somemessage";
            var function = new Mock<Func<string, Task>>();

            // act
            this.subject.Subscribe(lnsName, function.Object);
            await PublishAsync(lnsName, message);

            // assert
            function.Verify(a => a.Invoke(message), Times.Once);
        }

        [Fact]
        public async Task Subscribe_On_Different_Channel_Does_Not_Receive_Message()
        {
            // arrange
            var function = new Mock<Func<string, Task>>();

            // act
            this.subject.Subscribe("lns-1", function.Object);
            await PublishAsync("lns-2", string.Empty);

            // assert
            function.Verify(a => a.Invoke(It.IsAny<string>()), Times.Never);
        }

        private async Task PublishAsync(string channel, string message)
        {
            await this.redis.GetSubscriber().PublishAsync(channel, message);
        }
    }
}
