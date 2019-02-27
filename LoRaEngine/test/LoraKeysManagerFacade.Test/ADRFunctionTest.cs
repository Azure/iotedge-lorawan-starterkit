// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using Moq;
    using Xunit;

    public class ADRFunctionTest : FunctionTestBase
    {
        private static readonly ILoRaADRManager AdrManager;

        static ADRFunctionTest()
        {
            var strategyProvider = new Mock<ILoRaADRStrategyProvider>(MockBehavior.Strict);
            strategyProvider
                .Setup(x => x.GetStrategy())
                .Returns(new LoRaADRStandardStrategy());

            AdrManager = LoRaADRFunction.InitializeADRManager(new LoRaADRServerManager(new LoRaADRInMemoryStore(), strategyProvider.Object, string.Empty));
            LoRaDeviceCache.EnsureCacheStore(new LoRaInMemoryDeviceStore());
        }

        [Fact]
        public async Task ADR_First_Entry()
        {
            var deviceEUI = NewUniqueEUI64();
            var gatewayId = NewUniqueEUI64();

            var req = CreateStandardADRRequest(gatewayId);

            var result = await LoRaADRFunction.HandleADRRequest(deviceEUI, req, string.Empty);
            Assert.NotNull(result);
            Assert.Equal(1, result.NumberOfFrames);
            Assert.False(result.CanConfirmToDevice);
        }

        [Fact]
        public async Task ADR_MultiGateway_Entry_Update()
        {
            var deviceEUI = NewUniqueEUI64();

            var gateway1Id = NewUniqueEUI64();
            var gateway2Id = NewUniqueEUI64();

            var req = CreateStandardADRRequest(gateway1Id, -10);
            var req2 = CreateStandardADRRequest(gateway2Id, -10);

            _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req, string.Empty);
            _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req2, string.Empty);

            // last entry should have 2 and the id should be from gw1
            var last = await AdrManager.GetLastEntryAsync(deviceEUI);
            Assert.Equal(2, last.GatewayCount);
            Assert.Equal(gateway1Id, last.GatewayId);

            // reply
            _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req, string.Empty);
            _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req2, string.Empty);

            last = await AdrManager.GetLastEntryAsync(deviceEUI);
            Assert.Equal(4, last.GatewayCount);

            // add new fcnt and change snr for gw2
            req2.FCntUp = ++req.FCntUp;
            req2.RequiredSnr = -9;
            _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req, string.Empty);
            _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req2, string.Empty);

            last = await AdrManager.GetLastEntryAsync(deviceEUI);
            Assert.Equal(2, last.GatewayCount);
            Assert.Equal(gateway2Id, last.GatewayId);
            Assert.Equal(-9, last.Snr);
        }

        [Fact]
        public async Task ADR_MultiGateway_Full_Frame()
        {
            var deviceEUI = NewUniqueEUI64();

            var gateway1Id = NewUniqueEUI64();
            var gateway2Id = NewUniqueEUI64();

            var req = CreateStandardADRRequest(gateway1Id, -10);
            var req2 = CreateStandardADRRequest(gateway2Id, -10);

            var rnd = new Random();

            // add just 1 under the limit to the table
            for (var i = 0; i < LoRaADRTable.FrameCountCaptureCount - 1; i++)
            {
                _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req, string.Empty);
                _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req2, string.Empty);

                var winner = req.RequiredSnr < req2.RequiredSnr ? gateway2Id : gateway1Id;
                var last = await AdrManager.GetLastEntryAsync(deviceEUI);
                Assert.Equal(winner, last.GatewayId);
                Assert.Equal(2, last.GatewayCount);

                req.RequiredSnr = rnd.Next(-20, 5);
                req2.RequiredSnr = rnd.Next(-20, 5);
                req2.FCntUp = ++req.FCntUp;
            }

            req2.FCntUp = ++req.FCntUp;

            // first one should win
            var result1 = await LoRaADRFunction.HandleADRRequest(deviceEUI, req, string.Empty);
            var result2 = await LoRaADRFunction.HandleADRRequest(deviceEUI, req2, string.Empty);
            Assert.True(result1.CanConfirmToDevice);
            Assert.False(result2.CanConfirmToDevice);

            Assert.Equal(req.FCntDown + 1, result1.FCntDown);
            Assert.Equal(0, result2.FCntDown);
        }
    }
}
