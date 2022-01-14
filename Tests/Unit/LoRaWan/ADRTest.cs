// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1062 // Validate arguments of public methods

namespace LoRaWan.Tests.Unit
{
    using System.Collections.Generic;
    using global::LoRaTools.ADR;
    using global::LoRaTools.Regions;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;
    using static LoRaWan.DataRateIndex;

    public class ADRTest
    {
        private readonly ITestOutputHelper output;

        public ADRTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [ClassData(typeof(ADRTestData))]
        public async System.Threading.Tasks.Task TestADRAsync(string testName, DevEui devEUI, IList<LoRaADRTableEntry> tableEntries, RadioMetadata radioMetadata, bool expectDefaultAnswer, LoRaADRResult expectedResult)
        {
            this.output.WriteLine($"Starting test {testName}");
            var region = TestUtils.TestRegion;
            ILoRaADRStrategyProvider provider = new LoRaADRStrategyProvider(NullLoggerFactory.Instance);
            using var inMemoryStore = new LoRaADRInMemoryStore();
            var loRaADRManager = new Mock<LoRaADRManagerBase>(MockBehavior.Loose, inMemoryStore, provider, NullLogger<LoRaADRManagerBase>.Instance)
            {
                CallBase = true
            };
            _ = loRaADRManager.Setup(x => x.NextFCntDown(It.IsAny<DevEui>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>())).ReturnsAsync(1U);

            // If the test does not expect a default answer we trigger default reset before
            if (!expectDefaultAnswer)
            {
                _ = await loRaADRManager.Object.CalculateADRResultAndAddEntryAsync(devEUI, string.Empty, 1, 1, (float)region.RequiredSnr(radioMetadata.DataRate), radioMetadata.DataRate, region.TXPowertoMaxEIRP.Count - 1, region.MaxADRDataRate, new LoRaADRTableEntry()
                {
                    Snr = 0,
                    FCnt = 1,
                    DevEUI = devEUI,
                    GatewayCount = 1,
                    GatewayId = "gateway"
                });
            }

            for (var i = 0; i < tableEntries.Count; i++)
            {
                await loRaADRManager.Object.StoreADREntryAsync(tableEntries[i]);
            }

            var adrResult = await loRaADRManager.Object.CalculateADRResultAndAddEntryAsync(devEUI, string.Empty, 1, 1, (float)region.RequiredSnr(radioMetadata.DataRate), radioMetadata.DataRate, region.TXPowertoMaxEIRP.Count - 1, region.MaxADRDataRate);

            Assert.Equal(expectedResult.DataRate, adrResult.DataRate);
            Assert.Equal(expectedResult.NbRepetition, adrResult.NbRepetition);
            Assert.Equal(expectedResult.TxPower, adrResult.TxPower);
            Assert.Equal(expectedResult.FCntDown, adrResult.FCntDown);

            loRaADRManager.Verify(x => x.NextFCntDown(It.IsAny<DevEui>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>()), Times.AtLeastOnce, "NextFCntDown");
            this.output.WriteLine($"Test {testName} finished");
        }

        [Fact]
        public async System.Threading.Tasks.Task CheckADRReturnsDefaultValueIfCacheCrash()
        {
            var devEUI = TestEui.GenerateDevEui();
            var region = RegionManager.EU868;
            ILoRaADRStrategyProvider provider = new LoRaADRStrategyProvider(NullLoggerFactory.Instance);
            var datarate = DataRateIndex.DR5;
            using var inMemoryStore = new LoRaADRInMemoryStore();
            var loRaADRManager = new Mock<LoRaADRManagerBase>(MockBehavior.Loose, inMemoryStore, provider, NullLogger<LoRaADRManagerBase>.Instance)
            {
                CallBase = true
            };
            _ = loRaADRManager.Setup(x => x.NextFCntDown(It.IsAny<DevEui>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>())).ReturnsAsync(1U);

            // setup table with default value
            _ = await loRaADRManager.Object.CalculateADRResultAndAddEntryAsync(devEUI, string.Empty, 1, 1, (float)region.RequiredSnr(datarate), region.GetDownstreamDataRate(datarate), region.TXPowertoMaxEIRP.Count - 1, region.MaxADRDataRate, new LoRaADRTableEntry()
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

            var adrResult = await loRaADRManager.Object.CalculateADRResultAndAddEntryAsync(devEUI, string.Empty, 1, 1, (float)region.RequiredSnr(datarate), region.GetDownstreamDataRate(datarate), region.TXPowertoMaxEIRP.Count - 1, region.MaxADRDataRate);
            Assert.Equal(DR5, adrResult.DataRate);
            Assert.Equal(7, adrResult.TxPower);
            Assert.Equal(1, adrResult.NbRepetition);

            // reset cache and check we get default values
            _ = await loRaADRManager.Object.ResetAsync(devEUI);

            adrResult = await loRaADRManager.Object.CalculateADRResultAndAddEntryAsync(devEUI, string.Empty, 1, 1, (float)region.RequiredSnr(datarate), region.GetDownstreamDataRate(datarate), region.TXPowertoMaxEIRP.Count - 1, region.MaxADRDataRate, new LoRaADRTableEntry()
            {
                Snr = 0,
                FCnt = 1,
                DevEUI = devEUI,
                GatewayCount = 1,
                GatewayId = "gateway"
            });

            Assert.Equal(DR5, adrResult.DataRate);
            Assert.Equal(0, adrResult.TxPower);
            Assert.Equal(1, adrResult.NbRepetition);

            loRaADRManager.Verify(x => x.NextFCntDown(It.IsAny<DevEui>(), It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>()), Times.AtLeastOnce, "NextFCntDown");
        }
    }
}
