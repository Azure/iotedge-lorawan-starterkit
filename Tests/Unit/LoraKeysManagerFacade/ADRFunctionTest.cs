// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade.FunctionBundler
{
    using System;
    using System.Threading.Tasks;
    using global::LoraKeysManagerFacade;
    using global::LoraKeysManagerFacade.FunctionBundler;
    using global::LoRaTools.ADR;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class ADRFunctionTest : FunctionTestBase, IDisposable
    {
        private readonly LoRaADRInMemoryStore adrStore;
        private readonly ILoRaADRManager adrManager;
        private readonly ADRExecutionItem adrExecutionItem;

        public ADRFunctionTest()
        {
            var adrStrategy = new Mock<ILoRaADRStrategy>(MockBehavior.Strict);
            adrStrategy
                .Setup(x => x.ComputeResult(It.IsNotNull<LoRaADRTable>(), It.IsAny<float>(), It.IsAny<DataRateIndex>(), It.IsAny<int>(), It.IsAny<DataRateIndex>()))
                .Returns((LoRaADRTable table, double snr, int dr, int power, int maxDr) =>
                {
                    return table.Entries.Count >= LoRaADRTable.FrameCountCaptureCount
                    ? new LoRaADRResult()
                    {
                        NumberOfFrames = table.Entries.Count
                    }
                    : null;
                });

            adrStrategy.Setup(x => x.DefaultNbRep).Returns(1);
            adrStrategy.Setup(x => x.DefaultTxPower).Returns(0);
            adrStrategy.Setup(x => x.MinimumNumberOfResult).Returns(20);

            var strategyProvider = new Mock<ILoRaADRStrategyProvider>(MockBehavior.Strict);

            strategyProvider
                .Setup(x => x.GetStrategy())
                .Returns(adrStrategy.Object);

            this.adrStore = new LoRaADRInMemoryStore();
            this.adrManager = new LoRaADRServerManager(this.adrStore,
                                                       strategyProvider.Object,
                                                       new LoRaInMemoryDeviceStore(),
                                                       NullLoggerFactory.Instance,
                                                       NullLogger<LoRaADRServerManager>.Instance);
            this.adrExecutionItem = new ADRExecutionItem(this.adrManager);
        }

        [Fact]
        public async Task ADR_First_Entry_Device_Reset()
        {
            var deviceEUI = TestEui.GenerateDevEui();
            var gatewayId = NewUniqueEUI64();

            var req = CreateStandardADRRequest(gatewayId);

            var result = await this.adrExecutionItem.HandleADRRequest(deviceEUI, req);
            Assert.NotNull(result);
            Assert.Equal(1, result.NumberOfFrames);
            Assert.True(result.CanConfirmToDevice);

            Assert.Equal(0, result.TxPower);
            Assert.Equal(req.DataRate, result.DataRate);
            Assert.Equal(1, result.NbRepetition);
        }

        [Fact]
        public async Task ADR_MultiGateway_Entry_Update()
        {
            var deviceEUI = TestEui.GenerateDevEui();

            var gateway1Id = NewUniqueEUI64();
            var gateway2Id = NewUniqueEUI64();

            var req = CreateStandardADRRequest(gateway1Id, -10);
            var req2 = CreateStandardADRRequest(gateway2Id, -10);

            _ = await this.adrExecutionItem.HandleADRRequest(deviceEUI, req);
            _ = await this.adrExecutionItem.HandleADRRequest(deviceEUI, req2);

            // last entry should have 2 and the id should be from gw1
            var last = await this.adrManager.GetLastEntryAsync(deviceEUI);
            Assert.Equal(2U, last.GatewayCount);
            Assert.Equal(gateway1Id, last.GatewayId);

            // reply
            _ = await this.adrExecutionItem.HandleADRRequest(deviceEUI, req);
            _ = await this.adrExecutionItem.HandleADRRequest(deviceEUI, req2);

            last = await this.adrManager.GetLastEntryAsync(deviceEUI);
            Assert.Equal(4U, last.GatewayCount);

            // add new fcnt and change snr for gw2
            req2.FCntUp = ++req.FCntUp;
            req2.RequiredSnr = -9;
            _ = await this.adrExecutionItem.HandleADRRequest(deviceEUI, req);
            _ = await this.adrExecutionItem.HandleADRRequest(deviceEUI, req2);

            last = await this.adrManager.GetLastEntryAsync(deviceEUI);
            Assert.Equal(2U, last.GatewayCount);
            Assert.Equal(gateway2Id, last.GatewayId);
            Assert.Equal(-9, last.Snr);
        }

        [Fact]
        public async Task ADR_MultiGateway_Full_Frame()
        {
            var deviceEUI = TestEui.GenerateDevEui();

            var gateway1Id = NewUniqueEUI64();
            var gateway2Id = NewUniqueEUI64();

            var req1 = CreateStandardADRRequest(gateway1Id, -10);
            var req2 = CreateStandardADRRequest(gateway2Id, -10);

            var rnd = new Random();

            // add just 1 under the limit to the table
            for (var i = 0; i < LoRaADRTable.FrameCountCaptureCount - 1; i++)
            {
                var res1 = await this.adrExecutionItem.HandleADRRequest(deviceEUI, req1);
                var res2 = await this.adrExecutionItem.HandleADRRequest(deviceEUI, req2);

                var winner = req1.RequiredSnr < req2.RequiredSnr ? gateway2Id : gateway1Id;
                var last = await this.adrManager.GetLastEntryAsync(deviceEUI);
                Assert.Equal(winner, last.GatewayId);
                Assert.Equal(2U, last.GatewayCount);

                req1.RequiredSnr = rnd.Next(-20, 5);
                req2.RequiredSnr = rnd.Next(-20, 5);
                req2.FCntUp = ++req1.FCntUp;
                req1.FCntDown = res1.FCntDown.GetValueOrDefault() > 0 ? res1.FCntDown.Value : req1.FCntDown;
                req2.FCntDown = res2.FCntDown.GetValueOrDefault() > 0 ? res2.FCntDown.Value : req2.FCntDown;
            }

            req2.FCntUp = ++req1.FCntUp;

            // first one should win
            var result1 = await this.adrExecutionItem.HandleADRRequest(deviceEUI, req1);
            var result2 = await this.adrExecutionItem.HandleADRRequest(deviceEUI, req2);
            Assert.True(result1.CanConfirmToDevice);
            Assert.False(result2.CanConfirmToDevice);

            Assert.Equal(req1.FCntDown + 1, result1.FCntDown);
            Assert.Equal(0U, result2.FCntDown.GetValueOrDefault());
        }

        public void Dispose() => this.adrStore.Dispose();
    }
}
