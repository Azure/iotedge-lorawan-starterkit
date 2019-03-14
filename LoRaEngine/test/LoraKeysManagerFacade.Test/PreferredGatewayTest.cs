// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.FunctionBundler;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class PreferredGatewayTest
    {
        private readonly ILoRaDeviceCacheStore cache;
        private readonly PreferredGatewayExecutionItem preferredGatewayExecutionItem;

        public PreferredGatewayTest()
        {
            this.cache = new LoRaInMemoryDeviceStore();
            this.preferredGatewayExecutionItem = new PreferredGatewayExecutionItem(this.cache, new NullLogger<PreferredGatewayExecutionItem>(), null);
        }

        [Fact]
        public async Task When_Called_By_Multiple_Gateways_Should_Return_Closest()
        {
            var devEUI = Guid.NewGuid().ToString();
            const uint fcntUp = 1;

            var req1 = new FunctionBundlerRequest() { GatewayId = "gateway1", ClientFCntUp = fcntUp, Rssi = -180 };
            var pipeline1 = new FunctionBundlerPipelineExecuter(new IFunctionBundlerExecutionItem[] { this.preferredGatewayExecutionItem }, devEUI, req1);
            var t1 = Task.Run(() => this.preferredGatewayExecutionItem.ExecuteAsync(pipeline1));

            var req2 = new FunctionBundlerRequest() { GatewayId = "gateway2", ClientFCntUp = fcntUp, Rssi = -179 };
            var pipeline2 = new FunctionBundlerPipelineExecuter(new IFunctionBundlerExecutionItem[] { this.preferredGatewayExecutionItem }, devEUI, req2);
            var t2 = Task.Run(() => this.preferredGatewayExecutionItem.ExecuteAsync(pipeline2));

            var req3 = new FunctionBundlerRequest() { GatewayId = "gateway3", ClientFCntUp = fcntUp, Rssi = -39 };
            var pipeline3 = new FunctionBundlerPipelineExecuter(new IFunctionBundlerExecutionItem[] { this.preferredGatewayExecutionItem }, devEUI, req3);
            var t3 = Task.Run(() => this.preferredGatewayExecutionItem.ExecuteAsync(pipeline3));

            await Task.WhenAll(t1, t2, t3);

            foreach (var reqAndPipeline in new[] { (t1, pipeline1), (t2, pipeline2), (t3, pipeline3) })
            {
                Assert.True(reqAndPipeline.Item1.IsCompletedSuccessfully);
                var res = reqAndPipeline.Item1.Result;
                var pipeline = reqAndPipeline.Item2;
                Assert.Equal(FunctionBundlerExecutionState.Continue, res);
                Assert.NotNull(pipeline.Result.PreferredGatewayResult);
                Assert.Equal("gateway3", pipeline.Result.PreferredGatewayResult.PreferredGatewayID);
                Assert.Equal(fcntUp, pipeline.Result.PreferredGatewayResult.RequestFcntUp);
                Assert.Equal(fcntUp, pipeline.Result.PreferredGatewayResult.CurrentFcntUp);
                Assert.False(pipeline.Result.PreferredGatewayResult.Conflict);
                Assert.Null(pipeline.Result.PreferredGatewayResult.ErrorMessage);
            }
        }

        [Fact]
        public async Task When_Calling_Outdated_Fcnt_Should_Return_Conflict()
        {
            var devEUI = Guid.NewGuid().ToString();
            const uint fcntUp = 1;

            var req1 = new FunctionBundlerRequest() { GatewayId = "gateway1", ClientFCntUp = fcntUp + 1, Rssi = -180 };
            var pipeline1 = new FunctionBundlerPipelineExecuter(new IFunctionBundlerExecutionItem[] { this.preferredGatewayExecutionItem }, devEUI, req1);
            await this.preferredGatewayExecutionItem.ExecuteAsync(pipeline1);

            var req2 = new FunctionBundlerRequest() { GatewayId = "gateway2", ClientFCntUp = fcntUp, Rssi = -90 };
            var pipeline2 = new FunctionBundlerPipelineExecuter(new IFunctionBundlerExecutionItem[] { this.preferredGatewayExecutionItem }, devEUI, req2);
            var res2 = await this.preferredGatewayExecutionItem.ExecuteAsync(pipeline2);

            Assert.Equal(FunctionBundlerExecutionState.Continue, res2);
            Assert.NotNull(pipeline2.Result.PreferredGatewayResult);
            Assert.Equal("gateway1", pipeline2.Result.PreferredGatewayResult.PreferredGatewayID);
            Assert.Equal(fcntUp, pipeline2.Result.PreferredGatewayResult.RequestFcntUp);
            Assert.Equal(fcntUp + 1, pipeline2.Result.PreferredGatewayResult.CurrentFcntUp);
            Assert.True(pipeline2.Result.PreferredGatewayResult.Conflict);
        }

        [Fact]
        public async Task When_Calling_After_Delay_Should_Return_First_Gateway()
        {
            var devEUI = Guid.NewGuid().ToString();
            const uint staleFcntUp = 1;
            const uint currentFcntUp = 2;

            var req1 = new FunctionBundlerRequest() { GatewayId = "gateway1", ClientFCntUp = currentFcntUp, Rssi = -180 };
            var pipeline1 = new FunctionBundlerPipelineExecuter(new IFunctionBundlerExecutionItem[] { this.preferredGatewayExecutionItem }, devEUI, req1);
            await this.preferredGatewayExecutionItem.ExecuteAsync(pipeline1);

            var req2 = new FunctionBundlerRequest() { GatewayId = "gateway2", ClientFCntUp = staleFcntUp, Rssi = -90 };
            var pipeline2 = new FunctionBundlerPipelineExecuter(new IFunctionBundlerExecutionItem[] { this.preferredGatewayExecutionItem }, devEUI, req2);
            var res2 = await this.preferredGatewayExecutionItem.ExecuteAsync(pipeline2);

            var t1 = Task.Run(() => this.preferredGatewayExecutionItem.ExecuteAsync(pipeline1));

            await Task.Delay(PreferredGatewayExecutionItem.DEFAULT_RECEIVE_REQUESTS_PERIOD_IN_MS + 50);

            var t2 = Task.Run(() => this.preferredGatewayExecutionItem.ExecuteAsync(pipeline2));

            await Task.WhenAll(t1, t2);

            foreach (var resAndPipeline in new[] { (t1, pipeline1), (t2, pipeline2) })
            {
                Assert.True(resAndPipeline.Item1.IsCompletedSuccessfully);
                var res = resAndPipeline.Item1.Result;
                var pipeline = resAndPipeline.Item2;

                Assert.Equal(FunctionBundlerExecutionState.Continue, res);
                Assert.NotNull(pipeline.Result.PreferredGatewayResult);
                Assert.Equal("gateway1", pipeline.Result.PreferredGatewayResult.PreferredGatewayID);
                Assert.Equal(pipeline.Request.ClientFCntUp, pipeline.Result.PreferredGatewayResult.RequestFcntUp);
                Assert.Equal(currentFcntUp, pipeline.Result.PreferredGatewayResult.CurrentFcntUp);

                if (pipeline.Request.ClientFCntUp == staleFcntUp)
                    Assert.True(pipeline.Result.PreferredGatewayResult.Conflict);
                else
                    Assert.False(pipeline.Result.PreferredGatewayResult.Conflict);
            }
        }
    }
}
