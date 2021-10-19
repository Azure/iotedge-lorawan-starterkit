// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Moq;
    using Xunit;

    public class DeduplicationStrategyTest : MessageProcessorTestBase
    {
        private readonly Mock<LoRaDeviceAPIServiceBase> loRaDeviceApi;
        private readonly DeduplicationStrategyFactory factory;
        private readonly Mock<ILoRaDeviceClient> loRaDeviceClient;

        public DeduplicationStrategyTest()
        {
            this.loRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            this.factory = new DeduplicationStrategyFactory(this.loRaDeviceApi.Object);
            this.loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            this.loRaDeviceClient.Setup(ldc => ldc.Dispose());
            this.loRaDeviceApi.Setup(x => x.CheckDuplicateMsgAsync(It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<uint>()))
                .Returns(() =>
                {
                    return Task.FromResult<DeduplicationResult>(new DeduplicationResult
                    {
                        IsDuplicate = true
                    });
                });
        }

        [Fact]
        public async Task Validate_Drop_Strategy()
        {
            using var connectionManagerWrapper = TestUtils.CreateConnectionManager();
            var connectionManager = connectionManagerWrapper.Value;
            using var target = new LoRaDevice("1231", "12312", connectionManager);
            target.Deduplication = DeduplicationMode.Drop;
            var strategy = this.factory.Create(target);

            connectionManager.Register(target, this.loRaDeviceClient.Object);

            Assert.IsType<DeduplicationStrategyDrop>(strategy);
            var result = await strategy.ResolveDeduplication(1, 1, "12345");
            Assert.False(result.CanProcess);
        }

        [Fact]
        public async Task Validate_Mark_Strategy()
        {
            using var connectionManagerWrapper = TestUtils.CreateConnectionManager();
            var connectionManager = connectionManagerWrapper.Value;
            using var target = new LoRaDevice("1231", "12312", connectionManager);
            target.Deduplication = DeduplicationMode.Mark;

            connectionManager.Register(target, this.loRaDeviceClient.Object);

            var strategy = this.factory.Create(target);

            Assert.IsType<DeduplicationStrategyMark>(strategy);
            var result = await strategy.ResolveDeduplication(1, 1, "12345");
            Assert.True(result.CanProcess);
        }
    }
}
