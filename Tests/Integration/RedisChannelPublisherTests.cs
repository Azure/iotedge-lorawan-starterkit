namespace LoRaWan.Tests.Integration{

    using System;
    using System.Threading.Tasks;
    using StackExchange.Redis;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging;


        public class RedisChannelPublisherTests : FunctionTestBase, IClassFixture<RedisFixture>
    {
        private readonly IChannelPublisher channelPublisher;
        private readonly ITestOutputHelper testOutputHelper;

        public RedisChannelPublisherTests(RedisFixture redis, ITestOutputHelper testOutputHelper)
        {
            if (redis is null) throw new ArgumentNullException(nameof(redis));
            this.channelPublisher = new RedisChannelPublisher(redis.Redis, NullLogger.instance);
            this.testOutputHelper = testOutputHelper;
        }

        public async Task Publish_Aysnc(){
            var message = "test message";
            var channel = "channel1";
            Console.WriteLine("Publishing message...");
            channelPublisher.PublishAsync(channel,message);

        }
}
}