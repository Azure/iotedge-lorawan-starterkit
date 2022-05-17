namespace LoraKeysManagerFacade
{

    using StackExchange.Redis;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
        public class RedisChannelPublisher : IChannelPublisher
        {
            private ConnectionMultiplexer redis;
            private readonly ISubscriber subscriber;
            private readonly ILogger log;

            public RedisChannelPublisher(ConnectionMultiplexer redis, ILogger log){
                this.redis = redis;
                this.log = log;
                this.subscriber = this.redis.GetSubscriber();
                }


                public async Task PublishAsync(string channel, string value){
                await subscriber.PublishAsync(channel, value);
                }


    }
    
}