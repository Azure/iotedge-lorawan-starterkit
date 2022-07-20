// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade;
    using LoraKeysManagerFacade.FunctionBundler;
    using LoRaTools;
    using LoRaWan.Tests.Common;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.Azure.Devices;
    using Moq;
    using Xunit;

    /// <summary>
    /// Tests to run against a real Redis instance.
    /// </summary>
    [Collection(RedisFixture.CollectionName)]
    public sealed class DeduplicationTestWithRedis : IClassFixture<RedisFixture>, IDisposable
    {
        private readonly ILoRaDeviceCacheStore cache;
        private readonly Mock<IServiceClient> serviceClientMock;
        private readonly TelemetryConfiguration telemetryConfiguration;
        private readonly Mock<IEdgeDeviceGetter> edgeDeviceGetter;
        private readonly Mock<IChannelPublisher> channelPublisher;
        private readonly DeduplicationExecutionItem deduplicationExecutionItem;

        public DeduplicationTestWithRedis(RedisFixture redis)
        {
            if (redis is null) throw new ArgumentNullException(nameof(redis));

            this.cache = new LoRaDeviceCacheRedisStore(redis.Database);
            this.serviceClientMock = new Mock<IServiceClient>();
            this.telemetryConfiguration = new TelemetryConfiguration();
            this.edgeDeviceGetter = new Mock<IEdgeDeviceGetter>();
            this.channelPublisher = new Mock<IChannelPublisher>();
            this.deduplicationExecutionItem = new DeduplicationExecutionItem(this.cache,
                                                                             this.serviceClientMock.Object,
                                                                             this.edgeDeviceGetter.Object,
                                                                             this.channelPublisher.Object,
                                                                             this.telemetryConfiguration);
        }

        [Theory]
        [InlineData("gateway1", 1, "gateway1", 1, true)]
        [InlineData("gateway1", 1, "gateway1", 2, true)]
        [InlineData("gateway1", 1, "gateway2", 1, true)]
        [InlineData("gateway1", 1, "gateway2", 2, true)]
        [InlineData("gateway1", 1, "gateway1", 1, false)]
        [InlineData("gateway1", 1, "gateway1", 2, false)]
        [InlineData("gateway1", 1, "gateway2", 1, false)]
        [InlineData("gateway1", 1, "gateway2", 2, false)]
        public async Task When_Called_Multiple_Times_With_Same_Device_Should_Detect_Duplicates_Direct_Method_Or_Pub_Sub(string gateway1, uint fcnt1, string gateway2, uint fcnt2, bool isEdgeDevice)
        {
            this.edgeDeviceGetter.Setup(m => m.IsEdgeDeviceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(isEdgeDevice);
            this.serviceClientMock.Setup(
                x => x.InvokeDeviceMethodAsync(It.IsAny<string>(), Constants.NetworkServerModuleId, It.IsAny<CloudToDeviceMethod>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CloudToDeviceMethodResult() { Status = 200 });

            var devEUI = TestEui.GenerateDevEui();

            var req1 = new FunctionBundlerRequest() { GatewayId = gateway1, ClientFCntUp = fcnt1, ClientFCntDown = fcnt1 };
            var pipeline1 = new FunctionBundlerPipelineExecuter(new IFunctionBundlerExecutionItem[] { this.deduplicationExecutionItem }, devEUI, req1);
            var res1 = await this.deduplicationExecutionItem.ExecuteAsync(pipeline1);

            Assert.Equal(FunctionBundlerExecutionState.Continue, res1);
            Assert.NotNull(pipeline1.Result.DeduplicationResult);
            Assert.False(pipeline1.Result.DeduplicationResult.IsDuplicate);

            var req2 = new FunctionBundlerRequest() { GatewayId = gateway2, ClientFCntUp = fcnt2, ClientFCntDown = fcnt2 };
            var pipeline2 = new FunctionBundlerPipelineExecuter(new IFunctionBundlerExecutionItem[] { this.deduplicationExecutionItem }, devEUI, req2);
            var res2 = await this.deduplicationExecutionItem.ExecuteAsync(pipeline2);

            Assert.NotNull(pipeline2.Result.DeduplicationResult);

            // same gateway -> no duplicate
            if (gateway1 == gateway2)
            {
                Assert.Equal(FunctionBundlerExecutionState.Continue, res2);
                Assert.False(pipeline2.Result.DeduplicationResult.IsDuplicate);
            }
            // different gateway, the same fcnt -> duplicate
            else if (fcnt1 == fcnt2)
            {
                Assert.Equal(FunctionBundlerExecutionState.Abort, res2);
                Assert.True(pipeline2.Result.DeduplicationResult.IsDuplicate);
            }
            // different gateway, higher fcnt -> no duplicate
            else
            {
                Assert.Equal(FunctionBundlerExecutionState.Continue, res2);
                Assert.False(pipeline2.Result.DeduplicationResult.IsDuplicate);

                if (isEdgeDevice)
                {
                    // gateway1 should be notified that it needs to drop connection for the device
                    this.serviceClientMock.Verify(x => x.InvokeDeviceMethodAsync(gateway1, Constants.NetworkServerModuleId,
                        It.Is<CloudToDeviceMethod>(m => m.MethodName == LoraKeysManagerFacadeConstants.CloudToDeviceCloseConnection
                        && m.GetPayloadAsJson().Contains(devEUI.ToString())), It.IsAny<CancellationToken>()));
                }
                else
                {
                    this.channelPublisher.Verify(x => x.PublishAsync(gateway1, It.Is<LnsRemoteCall>(c => c.Kind == RemoteCallKind.CloseConnection)));
                    this.serviceClientMock.VerifyNoOtherCalls();
                }
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

        public void Dispose() =>
            this.telemetryConfiguration.Dispose();
    }
}
