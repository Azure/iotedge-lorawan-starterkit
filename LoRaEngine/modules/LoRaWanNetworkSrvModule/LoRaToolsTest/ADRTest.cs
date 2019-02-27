// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using System.Collections.Generic;
    using LoRaTools.ADR;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
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
        public async System.Threading.Tasks.Task TestADRAsync(string testName, string devEUI, List<LoRaADRTableEntry> tableEntries, Rxpk rxpk, LoRaADRResult expectedResult)
        {
            this.output.WriteLine($"Starting test {testName}");
            var region = RegionFactory.CreateEU868Region();
            ILoRaADRStrategyProvider provider = new LoRaADRStrategyProvider();

            var loRaADRManager = new LoRaADRManagerBase(new LoRaADRInMemoryStore(), provider);

            for (int i = 0; i < tableEntries.Count; i++)
            {
                await loRaADRManager.StoreADREntryAsync(tableEntries[i]);
            }

            var adrResult = await loRaADRManager.CalculateADRResultAndAddEntryAsync(devEUI, string.Empty, 1, 1, (float)rxpk.RequiredSnr, region.GetDRFromFreqAndChan(rxpk.Datr), region.TXPowertoMaxEIRP.Count - 1);
            if (expectedResult == null)
            {
                Assert.False(adrResult.CanConfirmToDevice);
            }
            else
            {
                Assert.Equal(expectedResult.DataRate, adrResult.DataRate);
                Assert.Equal(expectedResult.NbRepetition, adrResult.NbRepetition);
                Assert.Equal(expectedResult.TxPower, adrResult.TxPower);
                Assert.Equal(expectedResult.FCntDown, adrResult.FCntDown);
            }

            this.output.WriteLine($"Test {testName} finished");
        }
    }
}
