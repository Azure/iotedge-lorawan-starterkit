namespace LoRaWanTest
{
    using LoRaTools.Regions;
    using Xunit;

    public class RegionCN470Test : RegionTestBase
    {
        public RegionCN470Test()
        {
            Region = RegionManager.CN470;
        }

        [Theory]
        // 20 MHz plan A
        [InlineData(470.3, 483.9, 0)]
        [InlineData(476.5, 490.1, 1)]
        [InlineData(503.5, 490.3, 4)]
        [InlineData(504.5, 491.3, 5)]
        [InlineData(509.7, 496.5, 7)]
        // 20 MHz plan B
        [InlineData(476.9, 476.9, 8)]
        [InlineData(503.1, 503.1, 9)]
        // 26 MHz plan A
        [InlineData(470.3, 490.1, 10)]
        [InlineData(475.1, 490.1, 12)]
        [InlineData(471.1, 490.9, 14)]
        // 26 MHz plan B
        [InlineData(480.3, 500.1, 15)]
        [InlineData(489.7, 504.7, 17)]
        [InlineData(488.9, 503.9, 19)]
        public void TestFrequency(double inputFreq, double outputFreq, int joinChannel)
        {
            var rxpk = GenerateRxpk("SF12BW125", inputFreq);
            TestRegionFrequency(rxpk, outputFreq, joinChannel);
        }

        [Theory]
        [InlineData("SF12BW125", "SF12BW125", 0)]
        [InlineData("SF11BW125", "SF11BW125", 0)]
        [InlineData("SF10BW125", "SF10BW125", 0)]
        [InlineData("SF7BW500", "SF7BW500", 0)]
        [InlineData("SF10BW125", "SF11BW125", 1)]
        [InlineData("SF8BW125", "SF10BW125", 2)]
        [InlineData("SF7BW500", "SF9BW125", 3)]
        public void TestDataRate(string inputDr, string outputDr, int rx1DrOffset)
        {
            var rxpk = GenerateRxpk(inputDr, 470.3);
            TestRegionDataRate(rxpk, outputDr, rx1DrOffset);
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
