// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using System.Collections.Generic;
    using LoRaTools.ADR;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    public class ADRTest
    {
        ITestOutputHelper output;

        public ADRTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [ClassData(typeof(ADRTestData))]
        public async System.Threading.Tasks.Task TestADRAsync(string testName, string devEUI, List<LoRaADRTableEntry> tableEntries, Rxpk rxpk, bool expectDefaultAnswer, LoRaADRResult expectedResult)
        {
            this.output.WriteLine($"Starting test {testName}");
            var region = RegionManager.EU868;
            ILoRaADRStrategyProvider provider = new LoRaADRStrategyProvider();
            var loRaADRManager = new Mock<LoRaADRManagerBase>(MockBehavior.Loose, new LoRaADRInMemoryStore(), provider);
            loRaADRManager.CallBase = true;
            loRaADRManager.Setup(x => x.NextFCntDown(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>())).ReturnsAsync(1U);

            // If the test does not expect a default answer we trigger default reset before
            if (!expectDefaultAnswer)
            {
                _ = await loRaADRManager.Object.CalculateADRResultAndAddEntryAsync(devEUI, string.Empty, 1, 1, (float)rxpk.RequiredSnr, region.GetDRFromFreqAndChan(rxpk.Datr), region.TXPowertoMaxEIRP.Count - 1, region.MaxADRDataRate, new LoRaADRTableEntry()
                {
                    Snr = 0,
                    FCnt = 1,
                    DevEUI = devEUI,
                    GatewayCount = 1,
                    GatewayId = "gateway"
                });
            }

            for (int i = 0; i < tableEntries.Count; i++)
            {
                await loRaADRManager.Object.StoreADREntryAsync(tableEntries[i]);
            }

            var adrResult = await loRaADRManager.Object.CalculateADRResultAndAddEntryAsync(devEUI, string.Empty, 1, 1, (float)rxpk.RequiredSnr, region.GetDRFromFreqAndChan(rxpk.Datr), region.TXPowertoMaxEIRP.Count - 1, region.MaxADRDataRate);

            Assert.Equal(expectedResult.DataRate, adrResult.DataRate);
            Assert.Equal(expectedResult.NbRepetition, adrResult.NbRepetition);
            Assert.Equal(expectedResult.TxPower, adrResult.TxPower);
            Assert.Equal(expectedResult.FCntDown, adrResult.FCntDown);

            loRaADRManager.Verify(x => x.NextFCntDown(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>()), Times.AtLeastOnce, "NextFCntDown");
            this.output.WriteLine($"Test {testName} finished");
        }

        [Fact]
        public async System.Threading.Tasks.Task CheckADRReturnsDefaultValueIfCacheCrash()
        {
            string devEUI = "myloratest";
            var region = RegionManager.EU868;
            ILoRaADRStrategyProvider provider = new LoRaADRStrategyProvider();
            Rxpk rxpk = new Rxpk();
            rxpk.Datr = "SF7BW125";
            var loRaADRManager = new Mock<LoRaADRManagerBase>(MockBehavior.Loose, new LoRaADRInMemoryStore(), provider);
            loRaADRManager.CallBase = true;
            loRaADRManager.Setup(x => x.NextFCntDown(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>())).ReturnsAsync(1U);

            // setup table with default value
            _ = await loRaADRManager.Object.CalculateADRResultAndAddEntryAsync(devEUI, string.Empty, 1, 1, (float)rxpk.RequiredSnr, region.GetDRFromFreqAndChan(rxpk.Datr), region.TXPowertoMaxEIRP.Count - 1, region.MaxADRDataRate, new LoRaADRTableEntry()
            {
                Snr = 0,
                FCnt = 1,
                DevEUI = devEUI,
                GatewayCount = 1,
                GatewayId = "gateway"
            });

            // Add measurement and compute new ADR
            for (uint i = 0; i < 21; i++)
            {
                await loRaADRManager.Object.StoreADREntryAsync(
                    new LoRaADRTableEntry()
                    {
                        DevEUI = devEUI,
                        FCnt = i,
                        GatewayCount = 1,
                        GatewayId = "mygateway",
                        Snr = 50
                    });
            }

            var adrResult = await loRaADRManager.Object.CalculateADRResultAndAddEntryAsync(devEUI, string.Empty, 1, 1, (float)rxpk.RequiredSnr, region.GetDRFromFreqAndChan(rxpk.Datr), region.TXPowertoMaxEIRP.Count - 1, region.MaxADRDataRate);
            Assert.Equal(5, adrResult.DataRate);
            Assert.Equal(7, adrResult.TxPower);
            Assert.Equal(1, adrResult.NbRepetition);

            // reset cache and check we get default values
            await loRaADRManager.Object.ResetAsync(devEUI);

            adrResult = await loRaADRManager.Object.CalculateADRResultAndAddEntryAsync(devEUI, string.Empty, 1, 1, (float)rxpk.RequiredSnr, region.GetDRFromFreqAndChan(rxpk.Datr), region.TXPowertoMaxEIRP.Count - 1, region.MaxADRDataRate, new LoRaADRTableEntry()
            {
                Snr = 0,
                FCnt = 1,
                DevEUI = devEUI,
                GatewayCount = 1,
                GatewayId = "gateway"
            });

            Assert.Equal(5, adrResult.DataRate);
            Assert.Equal(0, adrResult.TxPower);
            Assert.Equal(1, adrResult.NbRepetition);

            loRaADRManager.Verify(x => x.NextFCntDown(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>()), Times.AtLeastOnce, "NextFCntDown");
        }
    }
}
