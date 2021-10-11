namespace LoRaWanTest
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using Xunit;

    public abstract class RegionTestBase
    {
        protected Region _region;

        public void TestRegionFrequencyAndDataRate(string inputDr, double inputFreq, string outputDr, double outputFreq)
        {
            var rxpk = GenerateRxpk(inputDr, inputFreq);
            Assert.True(_region.TryGetDownstreamChannelFrequency(rxpk[0], out double frequency));
            Assert.Equal(frequency, outputFreq);
            Assert.Equal(_region.GetDownstreamDR(rxpk[0]), outputDr);
        }

        protected static List<Rxpk> GenerateRxpk(string dr, double freq)
        {
            string jsonUplink =
                @"{ ""rxpk"":[
                {
                    ""time"":""2013-03-31T16:21:17.528002Z"",
                    ""tmst"":3512348611,
                    ""chan"":2,
                    ""rfch"":0,
                    ""freq"":" + freq + @",
                    ""stat"":1,
                    ""modu"":""LORA"",
                    ""datr"":""" + dr + @""",
                    ""codr"":""4/6"",
                    ""rssi"":-35,
                    ""lsnr"":5.1,
                    ""size"":32,
                    ""data"":""AAQDAgEEAwIBBQQDAgUEAwItEGqZDhI=""
                }]}";

            var multiRxpkInput = Encoding.Default.GetBytes(jsonUplink);
            byte[] physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            List<Rxpk> rxpk = Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(multiRxpkInput).ToArray());
            return rxpk;
        }

        public void TestDownstreamRx2FrequencyAndDataRate(LoRaRegionType loRaRegion, string nwksrvrx2dr, double? nwksrvrx2freq, ushort? rx2drfromtwins, double expectedFreq, string expectedDr)
        {
            var devEui = "testDevice";
            RegionManager.TryTranslateToRegion(loRaRegion, out Region region);
            var datr = region.GetDownstreamRX2Datarate(devEui, nwksrvrx2dr, rx2drfromtwins);
            var freq = region.GetDownstreamRX2Freq(devEui, nwksrvrx2freq);
            Assert.Equal(expectedFreq, freq);
            Assert.Equal(expectedDr, datr);
        }
    }
}
