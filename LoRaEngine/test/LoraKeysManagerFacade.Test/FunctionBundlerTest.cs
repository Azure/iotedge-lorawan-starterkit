// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.FunctionBundler;
    using LoRaTools.ADR;
    using LoRaTools.CommonAPI;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public class FunctionBundlerTest : FunctionTestBase
    {
        private readonly ILoRaADRManager adrManager;
        private readonly FunctionBundlerFunction functionBundler;
        private readonly ADRExecutionItem adrExecutionItem;
        private readonly Random rnd = new Random();

        public FunctionBundlerTest()
        {
            var strategy = new Mock<ILoRaADRStrategy>(MockBehavior.Strict);
            strategy.Setup(x => x.DefaultNbRep).Returns(1);
            strategy.Setup(x => x.DefaultTxPower).Returns(0);
            strategy.Setup(x => x.MinimumNumberOfResult).Returns(20);
            strategy
                .Setup(x => x.ComputeResult(It.IsNotNull<string>(), It.IsAny<LoRaADRTable>(), It.IsAny<float>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns((string devEUI, LoRaADRTable table, float snr, int upstreamDr, int minTxPower, int maxDr) =>
                {
                    return new LoRaADRResult
                    {
                        CanConfirmToDevice = true,
                        DataRate = upstreamDr,
                        TxPower = 0
                    };
                });

            var strategyProvider = new Mock<ILoRaADRStrategyProvider>(MockBehavior.Strict);
            strategyProvider
                .Setup(x => x.GetStrategy())
                .Returns(strategy.Object);

            // .Returns(new LoRaADRStandardStrategy());
            var cacheStore = new LoRaInMemoryDeviceStore();
            this.adrManager = new LoRaADRServerManager(new LoRaADRInMemoryStore(), strategyProvider.Object, cacheStore);
            this.adrExecutionItem = new ADRExecutionItem(this.adrManager);

            var items = new IFunctionBundlerExecutionItem[]
            {
                new DeduplicationExecutionItem(cacheStore),
                this.adrExecutionItem,
                new NextFCntDownExecutionItem(new FCntCacheCheck(cacheStore)),
                new PreferredGatewayExecutionItem(cacheStore, new NullLogger<PreferredGatewayExecutionItem>(), null),
            };

            this.functionBundler = new FunctionBundlerFunction(items);
        }

        [Fact]
        public async Task FunctionBundler_All_Functions()
        {
            var gatewayId1 = NewUniqueEUI64();
            var devEUI = NewUniqueEUI64();

            var req = CreateStandardBundlerRequest(gatewayId1);

            var resp = await this.functionBundler.HandleFunctionBundlerInvoke(devEUI, req);
            Assert.NotNull(resp);
            Assert.NotNull(resp.AdrResult);
            Assert.False(resp.AdrResult.CanConfirmToDevice);
            Assert.Equal(1, resp.AdrResult.NumberOfFrames);

            Assert.NotNull(resp.DeduplicationResult);
            Assert.Equal(gatewayId1, resp.DeduplicationResult.GatewayId);
            Assert.False(resp.DeduplicationResult.IsDuplicate);

            Assert.Equal(req.ClientFCntDown + 1, resp.NextFCntDown);
        }

        [Fact]
        public async Task FunctionBundler_FCntDown_Only()
        {
            var gatewayId1 = NewUniqueEUI64();
            var devEUI = NewUniqueEUI64();

            var req = CreateStandardBundlerRequest(gatewayId1);
            req.AdrRequest = null;
            req.FunctionItems = FunctionBundlerItemType.FCntDown;

            var resp = await this.functionBundler.HandleFunctionBundlerInvoke(devEUI, req);
            Assert.NotNull(resp);
            Assert.Null(resp.AdrResult);

            Assert.Null(resp.DeduplicationResult);

            Assert.Equal(req.ClientFCntDown + 1, resp.NextFCntDown);
        }

        [Fact]
        public async Task FunctionBundler_DeDup_Only()
        {
            var gatewayId1 = NewUniqueEUI64();
            var devEUI = NewUniqueEUI64();

            var req = CreateStandardBundlerRequest(gatewayId1);
            req.AdrRequest = null;
            req.FunctionItems = FunctionBundlerItemType.Deduplication;

            var resp = await this.functionBundler.HandleFunctionBundlerInvoke(devEUI, req);
            Assert.NotNull(resp);
            Assert.Null(resp.AdrResult);

            Assert.NotNull(resp.DeduplicationResult);
            Assert.Equal(gatewayId1, resp.DeduplicationResult.GatewayId);
            Assert.False(resp.DeduplicationResult.IsDuplicate);

            Assert.Null(resp.NextFCntDown);
        }

        [Fact]
        public async Task FunctionBundler_Adr_Only()
        {
            var gatewayId1 = NewUniqueEUI64();
            var devEUI = NewUniqueEUI64();

            var req = CreateStandardBundlerRequest(gatewayId1);

            req.FunctionItems = FunctionBundlerItemType.ADR;

            var resp = await this.functionBundler.HandleFunctionBundlerInvoke(devEUI, req);
            Assert.NotNull(resp);
            Assert.NotNull(resp.AdrResult);
            Assert.True(resp.AdrResult.CanConfirmToDevice);
            Assert.Equal(req.AdrRequest.DataRate, resp.AdrResult.DataRate);
            Assert.Equal(0, resp.AdrResult.TxPower);
            Assert.Equal(1, resp.AdrResult.NumberOfFrames);

            Assert.Null(resp.DeduplicationResult);
            Assert.Equal(2U, resp.NextFCntDown);
        }

        [Fact]
        public async Task FunctionBundler_Adr_and_Dedup()
        {
            var gatewayId1 = NewUniqueEUI64();
            var gatewayId2 = NewUniqueEUI64();
            var devEUI = NewUniqueEUI64();

            var req = CreateStandardBundlerRequest(gatewayId1);
            var req2 = CreateStandardBundlerRequest(gatewayId2);

            req.FunctionItems = FunctionBundlerItemType.ADR | FunctionBundlerItemType.Deduplication;
            req2.FunctionItems = FunctionBundlerItemType.ADR | FunctionBundlerItemType.Deduplication;

            var resp = await this.functionBundler.HandleFunctionBundlerInvoke(devEUI, req);
            Assert.NotNull(resp);
            Assert.NotNull(resp.AdrResult);
            Assert.False(resp.AdrResult.CanConfirmToDevice);
            Assert.Equal(1, resp.AdrResult.NumberOfFrames);

            Assert.NotNull(resp.DeduplicationResult);
            Assert.Equal(gatewayId1, resp.DeduplicationResult.GatewayId);
            Assert.False(resp.DeduplicationResult.IsDuplicate);

            Assert.Null(resp.NextFCntDown);

            // with ADR frames
            await this.adrManager.ResetAsync(devEUI);

            await this.PrepareADRFrames(devEUI, LoRaADRTable.FrameCountCaptureCount - 1, req.AdrRequest);
            req.ClientFCntUp = req.AdrRequest.FCntUp;

            resp = await this.functionBundler.HandleFunctionBundlerInvoke(devEUI, req);

            Assert.NotNull(resp);
            Assert.NotNull(resp.AdrResult);
            Assert.Equal(LoRaADRTable.FrameCountCaptureCount, resp.AdrResult.NumberOfFrames);

            Assert.NotNull(resp.DeduplicationResult);
            Assert.Equal(gatewayId1, resp.DeduplicationResult.GatewayId);
            Assert.False(resp.DeduplicationResult.IsDuplicate);
            Assert.True(resp.AdrResult.CanConfirmToDevice || resp.NextFCntDown == null);

            // multi request
            req.AdrRequest.FCntUp = req2.AdrRequest.FCntUp = req2.ClientFCntUp = ++req.ClientFCntUp;
            req.AdrRequest.FCntDown = req2.AdrRequest.FCntDown = req2.ClientFCntDown = req.ClientFCntDown = resp.NextFCntDown.GetValueOrDefault();

            resp = await this.functionBundler.HandleFunctionBundlerInvoke(devEUI, req);
            Assert.NotNull(resp);
            Assert.NotNull(resp.AdrResult);

            Assert.NotNull(resp.DeduplicationResult);
            Assert.Equal(gatewayId1, resp.DeduplicationResult.GatewayId);
            Assert.False(resp.DeduplicationResult.IsDuplicate);

            Assert.True(resp.AdrResult.CanConfirmToDevice || resp.NextFCntDown == null);

            resp = await this.functionBundler.HandleFunctionBundlerInvoke(devEUI, req2);
            Assert.NotNull(resp);
            Assert.NotNull(resp.AdrResult);
            Assert.True(resp.AdrResult.CanConfirmToDevice || resp.NextFCntDown == null);

            Assert.NotNull(resp.DeduplicationResult);
            Assert.Equal(gatewayId1, resp.DeduplicationResult.GatewayId);
            Assert.True(resp.DeduplicationResult.IsDuplicate);
        }

        [Fact]
        public async Task FunctionBundler_Multi()
        {
            var gatewayId1 = NewUniqueEUI64();
            var gatewayId2 = NewUniqueEUI64();
            var gatewayId3 = NewUniqueEUI64();
            var gatewayId4 = NewUniqueEUI64();
            var devEUI = NewUniqueEUI64();

            var requests = new FunctionBundlerRequest[]
            {
                CreateStandardBundlerRequest(gatewayId1),
                CreateStandardBundlerRequest(gatewayId2),
                CreateStandardBundlerRequest(gatewayId3),
                CreateStandardBundlerRequest(gatewayId4)
            };

            foreach (var req in requests)
            {
                await this.PrepareADRFrames(devEUI, 20, req.AdrRequest);
                req.ClientFCntUp = req.AdrRequest.FCntUp;
                req.ClientFCntDown = req.AdrRequest.FCntDown;
            }

            var tasks = new List<Task>(requests.Length);
            var functionBundlerResults = new List<FunctionBundlerResult>(requests.Length);

            foreach (var req in requests)
            {
                tasks.Add(Task.Run(async () =>
                {
                    functionBundlerResults.Add(await this.ExecuteRequest(devEUI, req));
                }));
            }

            Task.WaitAll(tasks.ToArray());
            // only one request should be winning the race
            var winners = 0;
            var dups = 0;
            foreach (var res in functionBundlerResults)
            {
                if (!res.DeduplicationResult.IsDuplicate)
                {
                    Assert.NotNull(res.AdrResult);
                    Assert.True(res.AdrResult.CanConfirmToDevice);
                    Assert.Equal(res.AdrResult.FCntDown, res.NextFCntDown);
                    Assert.True(res.NextFCntDown.GetValueOrDefault() > 0);
                    winners++;
                }
                else
                {
                    Assert.NotNull(res.AdrResult);
                    Assert.False(res.AdrResult.CanConfirmToDevice);
                    Assert.Equal(0U, res.AdrResult.FCntDown.GetValueOrDefault());
                    Assert.Null(res.NextFCntDown);
                    dups++;
                }
            }

            Assert.Equal(1, winners);
            Assert.Equal(functionBundlerResults.Count - 1, dups);
        }

        private async Task<FunctionBundlerResult> ExecuteRequest(string devEUI, FunctionBundlerRequest req)
        {
            var result = await this.functionBundler.HandleFunctionBundlerInvoke(devEUI, req);
            lock (req)
            {
                req.ClientFCntUp++;
                if (req.AdrRequest != null)
                {
                    req.AdrRequest.FCntUp++;
                    Assert.Equal(req.AdrRequest.FCntUp, req.ClientFCntUp);
                }

                if (result.NextFCntDown.HasValue)
                {
                    req.ClientFCntDown = result.NextFCntDown.Value;
                    if (req.AdrRequest != null)
                    {
                        req.AdrRequest.FCntDown = req.ClientFCntDown;
                    }
                }
            }

            return result;
        }

        private static FunctionBundlerRequest CreateStandardBundlerRequest(string gatewayId)
        {
            return new FunctionBundlerRequest
            {
                AdrRequest = CreateStandardADRRequest(gatewayId),
                ClientFCntDown = 1,
                ClientFCntUp = 2,
                GatewayId = gatewayId,
                FunctionItems = FunctionBundlerItemType.ADR | FunctionBundlerItemType.Deduplication | FunctionBundlerItemType.FCntDown
            };
        }

        private async Task PrepareADRFrames(string deviceEUI, int numberOfFrames, LoRaADRRequest req)
        {
            await this.PrepareADRFrames(deviceEUI, numberOfFrames, new List<LoRaADRRequest>() { req });
        }

        private async Task PrepareADRFrames(string deviceEUI, int numberOfFrames, List<LoRaADRRequest> requests)
        {
            // add just 1 under the limit to the table
            for (var i = 0; i < numberOfFrames; i++)
            {
                foreach (var req in requests)
                {
                    var res = await this.adrExecutionItem.HandleADRRequest(deviceEUI, req);

                    lock (this.rnd)
                    {
                        req.RequiredSnr = this.rnd.Next(-20, 20);
                    }

                    req.DataRate = 2;
                    ++req.FCntUp;
                    req.FCntDown = res.FCntDown.GetValueOrDefault() > 0 ? res.FCntDown.Value : req.FCntDown;
                }
            }
        }

        [Fact]
        public void Execution_Items_Should_Have_Correct_Priority()
        {
            var cacheStore = new LoRaInMemoryDeviceStore();

            var items = new IFunctionBundlerExecutionItem[]
            {
                new DeduplicationExecutionItem(cacheStore),
                new ADRExecutionItem(this.adrManager),
                new NextFCntDownExecutionItem(new FCntCacheCheck(cacheStore)),
                new PreferredGatewayExecutionItem(cacheStore, new NullLogger<PreferredGatewayExecutionItem>(), null),
            };

            var sorted = items.OrderBy(x => x.Priority);
            Assert.IsType<DeduplicationExecutionItem>(sorted.ElementAt(0));
            Assert.IsType<ADRExecutionItem>(sorted.ElementAt(1));
            Assert.IsType<NextFCntDownExecutionItem>(sorted.ElementAt(2));
            Assert.IsType<PreferredGatewayExecutionItem>(sorted.ElementAt(3));

            // Ensure no item has the same priority
            Assert.Empty(items.GroupBy(x => x.Priority).Where(x => x.Count() > 1));
        }
    }
}
