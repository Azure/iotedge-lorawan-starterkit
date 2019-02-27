// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.FunctionBundler;
    using LoRaTools.ADR;
    using LoRaTools.CommonAPI;
    using Moq;
    using Xunit;

    public class FunctionBundlerTest : FunctionTestBase
    {
        private static readonly ILoRaADRManager AdrManager;

        static FunctionBundlerTest()
        {
            var strategyProvider = new Mock<ILoRaADRStrategyProvider>(MockBehavior.Strict);
            strategyProvider
                .Setup(x => x.GetStrategy())
                .Returns(new LoRaADRStandardStrategy());

            AdrManager = LoRaADRFunction.InitializeADRManager(new LoRaADRServerManager(new LoRaADRInMemoryStore(), strategyProvider.Object, string.Empty));
            LoRaDeviceCache.EnsureCacheStore(new LoRaInMemoryDeviceStore());
        }

        [Fact]
        public async Task FunctionBundler_All_Functions()
        {
            var gatewayId1 = NewUniqueEUI64();
            var devEUI = NewUniqueEUI64();

            var req = CreateStandardBundlerRequest(gatewayId1);

            var resp = await FunctionBundler.HandleFunctionBundlerInvoke(devEUI, req, string.Empty);
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
            req.FunctionItems = FunctionBundlerItem.FCntDown;

            var resp = await FunctionBundler.HandleFunctionBundlerInvoke(devEUI, req, string.Empty);
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
            req.FunctionItems = FunctionBundlerItem.Deduplication;

            var resp = await FunctionBundler.HandleFunctionBundlerInvoke(devEUI, req, string.Empty);
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

            req.FunctionItems = FunctionBundlerItem.ADR;

            var resp = await FunctionBundler.HandleFunctionBundlerInvoke(devEUI, req, string.Empty);
            Assert.NotNull(resp);
            Assert.NotNull(resp.AdrResult);
            Assert.False(resp.AdrResult.CanConfirmToDevice);
            Assert.Equal(1, resp.AdrResult.NumberOfFrames);

            Assert.Null(resp.DeduplicationResult);
            Assert.Null(resp.NextFCntDown);
        }

        [Fact]
        public async Task FunctionBundler_Adr_and_Dedup()
        {
            var gatewayId1 = NewUniqueEUI64();
            var gatewayId2 = NewUniqueEUI64();
            var devEUI = NewUniqueEUI64();

            var req = CreateStandardBundlerRequest(gatewayId1);
            var req2 = CreateStandardBundlerRequest(gatewayId2);

            req.FunctionItems = FunctionBundlerItem.ADR | FunctionBundlerItem.Deduplication;
            req2.FunctionItems = FunctionBundlerItem.ADR | FunctionBundlerItem.Deduplication;

            var resp = await FunctionBundler.HandleFunctionBundlerInvoke(devEUI, req, string.Empty);
            Assert.NotNull(resp);
            Assert.NotNull(resp.AdrResult);
            Assert.False(resp.AdrResult.CanConfirmToDevice);
            Assert.Equal(1, resp.AdrResult.NumberOfFrames);

            Assert.NotNull(resp.DeduplicationResult);
            Assert.Equal(gatewayId1, resp.DeduplicationResult.GatewayId);
            Assert.False(resp.DeduplicationResult.IsDuplicate);

            Assert.Null(resp.NextFCntDown);

            // with ADR frames
            await AdrManager.ResetAsync(devEUI);

            await PrepareADRFrames(devEUI, LoRaADRTable.FrameCountCaptureCount - 1, req.AdrRequest);
            req.ClientFCntUp = req.AdrRequest.FCntUp;

            resp = await FunctionBundler.HandleFunctionBundlerInvoke(devEUI, req, string.Empty);

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

            resp = await FunctionBundler.HandleFunctionBundlerInvoke(devEUI, req, string.Empty);
            Assert.NotNull(resp);
            Assert.NotNull(resp.AdrResult);

            Assert.NotNull(resp.DeduplicationResult);
            Assert.Equal(gatewayId1, resp.DeduplicationResult.GatewayId);
            Assert.False(resp.DeduplicationResult.IsDuplicate);

            Assert.True(resp.AdrResult.CanConfirmToDevice || resp.NextFCntDown == null);

            resp = await FunctionBundler.HandleFunctionBundlerInvoke(devEUI, req2, string.Empty);
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
                await PrepareADRFrames(devEUI, 20, req.AdrRequest);
                req.ClientFCntUp = req.AdrRequest.FCntUp;
                req.ClientFCntDown = req.AdrRequest.FCntDown;
            }

            var tasks = new List<Task>(requests.Length);
            var functionBundlerResults = new List<FunctionBundlerResult>(requests.Length);

            foreach (var req in requests)
            {
                // functionBundlerResults.Add(await ExecuteRequest(devEUI, req));
                tasks.Add(Task.Run(async () =>
                {
                    functionBundlerResults.Add(await ExecuteRequest(devEUI, req));
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
                    Assert.Equal(0, res.AdrResult.FCntDown);
                    Assert.Null(res.NextFCntDown);
                    dups++;
                }
            }

            Assert.Equal(1, winners);
            Assert.Equal(requests.Length - 1, dups);
        }

        private static async Task<FunctionBundlerResult> ExecuteRequest(string devEUI, FunctionBundlerRequest req)
        {
            var result = await FunctionBundler.HandleFunctionBundlerInvoke(devEUI, req, string.Empty);
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
                FunctionItems = FunctionBundlerItem.ADR | FunctionBundlerItem.Deduplication | FunctionBundlerItem.FCntDown
            };
        }

        private static async Task PrepareADRFrames(string deviceEUI, int numberOfFrames, LoRaADRRequest req)
        {
            await PrepareADRFrames(deviceEUI, numberOfFrames, new List<LoRaADRRequest>() { req });
        }

        private static async Task PrepareADRFrames(string deviceEUI, int numberOfFrames, List<LoRaADRRequest> requests)
        {
            var rnd = new Random();

            // add just 1 under the limit to the table
            for (var i = 0; i < numberOfFrames; i++)
            {
                foreach (var req in requests)
                {
                    var res = await LoRaADRFunction.HandleADRRequest(deviceEUI, req, string.Empty);

                    req.RequiredSnr = rnd.Next(-20, 20);
                    req.DataRate = 2;
                    ++req.FCntUp;
                    req.FCntDown = res.FCntDown > 0 ? res.FCntDown : req.FCntDown;
                }
            }
        }
    }
}
