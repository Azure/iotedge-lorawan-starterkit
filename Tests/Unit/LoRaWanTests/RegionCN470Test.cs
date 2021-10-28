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
            Assert.True(Region.TryGetJoinChannelIndex(freq, out var channelIndex));
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
            TestRegionFrequency(inputFreq, 0, outputFreq, joinChannel);
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(1, 1, 0)]
        [InlineData(2, 2, 0)]
        [InlineData(6, 6, 0)]
        [InlineData(2, 1, 1)]
        [InlineData(4, 2, 2)]
        [InlineData(6, 3, 3)]
        [InlineData(6, 6, 10)]
        public void TestDataRate(ushort inputDr, ushort outputDr, int rx1DrOffset)
        {
            var freq = 470.3;
            TestRegionDataRate(freq, inputDr, outputDr, rx1DrOffset);
        }

        [Theory]
        [InlineData(470, 0)]
        [InlineData(510, 1)]
        [InlineData(509, 100)]
        [InlineData(490, 110)]
        public void TestNotAllowedDataRates(double freq, ushort datarate)
        {
            TestRegionLimit(freq, datarate, 0);
        }

        [Theory]
        [InlineData(1, 31)]
        [InlineData(2, 94)]
        [InlineData(3, 192)]
        [InlineData(4, 250)]
        [InlineData(5, 250)]
        [InlineData(6, 250)]
        [InlineData(7, 250)]
        public void TestMaxPayloadLength(ushort datr, uint maxPyldSize)
        {
            TestRegionMaxPayloadLength(datr, maxPyldSize);
        }

        // TODO: expand test cases as part of #561
        [Theory]
        [InlineData(null, null, null, 485.3, (ushort)1)]
        [InlineData((ushort)1, null, null, 485.3, (ushort)1)]
        public void TestDownstreamRX2(ushort? nwksrvrx2dr, double? nwksrvrx2freq, ushort? rx2drfromtwins, double expectedFreq, ushort expectedDr)
        {
            TestDownstreamRX2FrequencyAndDataRate(nwksrvrx2dr, nwksrvrx2freq, rx2drfromtwins, expectedFreq, expectedDr);
        }

        [Fact]
        public void TestTranslateRegionType()
        {
            TestTranslateToRegion(LoRaRegionType.CN470);
        }

        [Fact]
        public void TestResolveRegion()
        {
            TestTryResolveRegion(470.3);
        }
    }
}
