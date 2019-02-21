// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using LoRaTools.ADR;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using Xunit;

    public class ADRTest
    {
        [Theory]
        [ClassData(typeof(ADRTestData))]
        public async System.Threading.Tasks.Task TestADRAsync(string devEUI, List<LoRaADRTableEntry> tableEntries, Rxpk rxpk, LoRaADRResult expectedResult)
        {
            var region = RegionFactory.CreateEU868Region();
            ILoRaADRStrategyProvider provider = new LoRaADRStrategyProvider();

            LoRAADRManagerFactory loRAADRManagerFactory = new LoRAADRManagerFactory();
            var loRaADRManager = loRAADRManagerFactory.Create(true, provider);

            for (int i = 0; i < tableEntries.Count; i++)
            {
                await loRaADRManager.StoreADREntry(tableEntries[i]);
            }

            var adrResult = await loRaADRManager.CalculateADRResult(devEUI, (float)rxpk.RequiredSnr, region.GetDRFromFreqAndChan(rxpk.Datr), region.TXPowertoMaxEIRP.Count - 1);
            if (expectedResult == null)
            {
                Assert.Null(adrResult);
            }
        }
    }
}
