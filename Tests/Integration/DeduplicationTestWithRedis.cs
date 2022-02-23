// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade;
    using LoraKeysManagerFacade.FunctionBundler;
    using LoRaWan.Tests.Common;
    using Moq;
    using Xunit;

    /// <summary>
    /// Tests to run against a real redis instance.
    /// </summary>
    [Collection(RedisFixture.CollectionName)]
    public class DeduplicationTestWithRedis : IClassFixture<RedisFixture>
    {
        private readonly ILoRaDeviceCacheStore cache;
        private readonly Mock<IServiceClient> serviceClientMock;
        private readonly DeduplicationExecutionItem deduplicationExecutionItem;

        public DeduplicationTestWithRedis(RedisFixture redis)
        {
            if (redis is null) throw new ArgumentNullException(nameof(redis));

            this.cache = new LoRaDeviceCacheRedisStore(redis.Database);
            this.serviceClientMock = new Mock<IServiceClient>();
            this.deduplicationExecutionItem = new DeduplicationExecutionItem(this.cache, this.serviceClientMock.Object);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(1, 2)]
        public async Task When_Called_By_Multiple_Gateways_Should_Detect_Duplicates(uint fcntUp1, uint fcntUp2)
        {
            var devEUI = TestEui.GenerateDevEui();

            var req1 = new FunctionBundlerRequest() { GatewayId = "gateway1", ClientFCntUp = fcntUp1, ClientFCntDown = fcntUp1 };
            var pipeline1 = new FunctionBundlerPipelineExecuter(new IFunctionBundlerExecutionItem[] { this.deduplicationExecutionItem }, devEUI, req1);
            var res1 = await this.deduplicationExecutionItem.ExecuteAsync(pipeline1);

            Assert.Equal(FunctionBundlerExecutionState.Continue, res1);
            Assert.NotNull(pipeline1.Result.DeduplicationResult);
            Assert.False(pipeline1.Result.DeduplicationResult.IsDuplicate);

            var req2 = new FunctionBundlerRequest() { GatewayId = "gateway2", ClientFCntUp = fcntUp2, ClientFCntDown = fcntUp2 };
            var pipeline2 = new FunctionBundlerPipelineExecuter(new IFunctionBundlerExecutionItem[] { this.deduplicationExecutionItem }, devEUI, req2);
            var res2 = await this.deduplicationExecutionItem.ExecuteAsync(pipeline2);

            Assert.NotNull(pipeline2.Result.DeduplicationResult);
            if (fcntUp1 == fcntUp2)
            {
                Assert.Equal(FunctionBundlerExecutionState.Abort, res2);
                Assert.True(pipeline2.Result.DeduplicationResult.IsDuplicate);
            }
            else
            {
                Assert.Equal(FunctionBundlerExecutionState.Continue, res2);
                Assert.False(pipeline2.Result.DeduplicationResult.IsDuplicate);
            }
        }

        [Fact]
        public async Task When_Called_With_Different_Devices_Should_Detect_No_Duplicates()
        {
            var devEUI1 = TestEui.GenerateDevEui();
            var devEUI2 = TestEui.GenerateDevEui();
            const uint fcnt = 1;

            var req1 = new FunctionBundlerRequest() { GatewayId = "gateway1", ClientFCntUp = fcnt, ClientFCntDown = fcnt };
            var pipeline1 = new FunctionBundlerPipelineExecuter(new IFunctionBundlerExecutionItem[] { this.deduplicationExecutionItem }, devEUI1, req1);
            var res1 = await this.deduplicationExecutionItem.ExecuteAsync(pipeline1);

            Assert.Equal(FunctionBundlerExecutionState.Continue, res1);
            Assert.NotNull(pipeline1.Result.DeduplicationResult);
            Assert.False(pipeline1.Result.DeduplicationResult.IsDuplicate);

            var req2 = new FunctionBundlerRequest() { GatewayId = "gateway2", ClientFCntUp = fcnt, ClientFCntDown = fcnt };
            var pipeline2 = new FunctionBundlerPipelineExecuter(new IFunctionBundlerExecutionItem[] { this.deduplicationExecutionItem }, devEUI2, req2);
            var res2 = await this.deduplicationExecutionItem.ExecuteAsync(pipeline2);

            Assert.Equal(FunctionBundlerExecutionState.Continue, res2);
            Assert.NotNull(pipeline2.Result.DeduplicationResult);
            Assert.False(pipeline2.Result.DeduplicationResult.IsDuplicate);
        }
    }
}
