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
        private readonly ILoRaADRManager adrManager;

        public ADRFunctionTest()
        {
            var strategyProvider = new Mock<ILoRaADRStrategyProvider>(MockBehavior.Strict);
            strategyProvider
                .Setup(x => x.GetStrategy())
                .Returns(new LoRaADRStandardStrategy());

            this.adrManager = new LoRaADRServerManager(new LoRaADRInMemoryStore(), strategyProvider.Object, string.Empty);
            LoRaADRFunction.InitializeADRManager(this.adrManager);
            LoRaDeviceCache.EnsureCacheStore(new LoRaInMemoryDeviceStore());
        }

        [Fact]
        public async Task ADR_First_Entry()
        {
            var deviceEUI = NewUniqueEUI64();
            var gatewayId = NewUniqueEUI64();

            var req = CreateStandardRequest(gatewayId);

            var result = await LoRaADRFunction.HandleADRRequest(deviceEUI, req, string.Empty);
            Assert.Null(result);
        }

        [Fact]
        public async Task ADR_MultiGateway_Entry_Update()
        {
            var deviceEUI = NewUniqueEUI64();

            var gateway1Id = NewUniqueEUI64();
            var gateway2Id = NewUniqueEUI64();

            var req = CreateStandardRequest(gateway1Id, -10);
            var req2 = CreateStandardRequest(gateway2Id, -10);

            _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req, string.Empty);
            _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req2, string.Empty);

            // last entry should have 2 and the id should be from gw1
            var last = await this.adrManager.GetLastEntry(deviceEUI);
            Assert.Equal(2, last.GatewayCount);
            Assert.Equal(gateway1Id, last.GatewayId);

            // reply
            _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req, string.Empty);
            _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req2, string.Empty);

            last = await this.adrManager.GetLastEntry(deviceEUI);
            Assert.Equal(4, last.GatewayCount);

            // add new fcnt and change snr for gw2
            req2.FCntUp = ++req.FCntUp;
            req2.RequiredSnr = -9;
            _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req, string.Empty);
            _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req2, string.Empty);

            last = await this.adrManager.GetLastEntry(deviceEUI);
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

            var req = CreateStandardRequest(gateway1Id, -10);
            var req2 = CreateStandardRequest(gateway2Id, -10);

            var rnd = new Random();

            // add just 1 under the limit to the table
            for (var i = 0; i < LoRaADRTable.FrameCountCaptureCount - 1; i++)
            {
                _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req, string.Empty);
                _ = await LoRaADRFunction.HandleADRRequest(deviceEUI, req2, string.Empty);

                var winner = req.RequiredSnr < req2.RequiredSnr ? gateway2Id : gateway1Id;
                var last = await this.adrManager.GetLastEntry(deviceEUI);
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

        private static LoRaADRRequest CreateStandardRequest(string gatewayId, float snr = -10)
        {
            var req = StandardRequest;
            req.GatewayId = gatewayId;
            req.RequiredSnr = snr;
            return req;
        }

        private static LoRaADRRequest StandardRequest
        {
            get
            {
                return new LoRaADRRequest
                {
                    DataRate = 1,
                    FCntUp = 1,
                    RequiredSnr = -10,
                    FCntDown = 1,
                    MinTxPowerIndex = 4,
                    PerformADRCalculation = true
                };
            }
        }
    }
}
