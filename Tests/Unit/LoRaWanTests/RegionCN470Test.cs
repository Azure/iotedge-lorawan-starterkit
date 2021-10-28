namespace LoRaWan.Tests.Unit.LoRaWanTests
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
        [InlineData(470.9, 0)]
        [InlineData(475.7, 3)]
        [InlineData(507.3, 6)]
        [InlineData(499.9, 9)]
        [InlineData(478.3, 14)]
        [InlineData(482.3, 16)]
        [InlineData(488.3, 19)]
        public void TestTryGetJoinChannelIndex(double freq, int expectedIndex)
        {
            var rxpk = GenerateRxpk("SF12BW125", freq);
            Assert.True(Region.TryGetJoinChannelIndex(rxpk[0], out var channelIndex));
            Assert.Equal(expectedIndex, channelIndex);
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
            TestRegionFrequency(rxpk, outputFreq, new DeviceJoinInfo(joinChannel));
        }

        [Theory]
        [InlineData("SF12BW125", "SF12BW125", 0)]
        [InlineData("SF11BW125", "SF11BW125", 0)]
        [InlineData("SF10BW125", "SF10BW125", 0)]
        [InlineData("SF7BW500", "SF7BW500", 0)]
        [InlineData("SF10BW125", "SF11BW125", 1)]
        [InlineData("SF8BW125", "SF10BW125", 2)]
        [InlineData("SF7BW500", "SF9BW125", 3)]
        [InlineData("SF7BW500", "SF7BW500", 10)]
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
            TestRegionLimit(freq, datarate, new DeviceJoinInfo(0));
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

        [Theory]
        // OTAA devices - join channel set in reported twin properties
        [InlineData(null, 0, null, 485.3)]
        [InlineData(null, 1, 9, 486.9)]
        [InlineData(null, 7, null, 496.5)]
        [InlineData(null, 9, 8, 498.3)]
        [InlineData(null, 10, null, 492.5)]
        [InlineData(null, 14, 14, 492.5)]
        [InlineData(null, 19, 18, 502.5)]
        [InlineData(498.3, 7, null, 498.3)]
        [InlineData(485.3, 15, null, 485.3)]
        // ABP devices
        [InlineData(null, null, 0, 486.9)]
        [InlineData(null, null, 7, 486.9)]
        [InlineData(null, null, 9, 498.3)]
        [InlineData(null, null, 14, 492.5)]
        [InlineData(null, null, 19, 502.5)]
        [InlineData(486.9, null, 12, 486.9)]
        [InlineData(502.5, null, 17, 502.5)]
        public void TestRX2Frequency(double? nwksrvrx2freq, int? reportedJoinChannel, int? desiredJoinChannel, double expectedFreq)
        {
            var deviceJoinInfo = new DeviceJoinInfo(reportedJoinChannel, desiredJoinChannel);
            TestDownstreamRX2Frequency(nwksrvrx2freq, expectedFreq, deviceJoinInfo);
        }

        [Fact]
        public void TestTranslateRegionType()
        {
            TestTranslateToRegion(LoRaRegionType.CN470);
        }

        [Fact]
        public void TestResolveRegion()
        {
            TestTryResolveRegion("SF11BW125", 470.3);
        }
    }
}
