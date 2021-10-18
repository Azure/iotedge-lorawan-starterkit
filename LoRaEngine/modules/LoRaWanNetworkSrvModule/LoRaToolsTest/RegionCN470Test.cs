namespace LoRaWanTest
{
    using LoRaTools.Regions;
    using Xunit;

    public class RegionCN470Test : RegionTestBase
    {
        public RegionCN470Test()
        {
            _region = RegionManager.CN470;
        }

        [Theory]
        [InlineData(470, "SF12BW125")]
        [InlineData(510, "SF11BW125")]
        [InlineData(509, "SF2BW125")]
        [InlineData(490, "SF30BW125")]
        public void TestLimit(double freq, string datarate)
        {
            TestRegionLimit(freq, datarate, 0);
        }

        [Theory]
        [InlineData("SF11BW125", 31)]
        [InlineData("SF10BW125", 94)]
        [InlineData("SF9BW125", 192)]
        [InlineData("SF8BW125", 250)]
        [InlineData("SF7BW125", 250)]
        [InlineData("SF7BW500", 250)]
        [InlineData("50", 250)]
        public void TestMaxPayloadLength(string datr, uint maxPyldSize)
        {
            TestRegionMaxPayloadLength(datr, maxPyldSize);
        }

        // TODO: expand test cases as part of #561
        [Theory]
        [InlineData("", null, null, 485.3, "SF11BW125")]
        [InlineData("SF11BW125", null, null, 485.3, "SF11BW125")]
        public void TestDownstreamRX2(string nwksrvrx2dr, double? nwksrvrx2freq, ushort? rx2drfromtwins, double expectedFreq, string expectedDr)
        {
            TestDownstreamRX2FrequencyAndDataRate(nwksrvrx2dr, nwksrvrx2freq, rx2drfromtwins, expectedFreq, expectedDr);
        }
    }
}
