namespace LoRaWanTest
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.Regions;

    public class RegionEU868Test : RegionTestBase
    {
        public RegionEU868Test()
        {
            _region = new RegionEU868();
        }

        protected override IEnumerable<(string inputDr, double inputFreq, string outputDr, double outputFreq)> CreateValidTestData()
        {
            throw new NotImplementedException();
        }
    }
}
