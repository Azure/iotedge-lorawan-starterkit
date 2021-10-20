namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using LoRaTools.Regions;
    using Xunit;

    public class RegionEU868Test : RegionTestBase
    {
        public RegionEU868Test()
        {
            Region = RegionManager.EU868;
        }

        [Theory]
        [CombinatorialData]
        public void TestFrequencyAndDataRate(
            [CombinatorialValues("SF12BW125", "SF11BW125", "SF10BW125", "SF9BW125", "SF8BW125", "SF7BW125", "SF7BW250")] string inputDr,
            [CombinatorialValues(868.1, 868.3, 868.5)] double inputFreq)
        {
            var expectedDr = inputDr;
            var expectedFreq = inputFreq;

            TestRegionFrequencyAndDataRate(inputDr, inputFreq, expectedDr, expectedFreq);
        }

        [Theory]
        [InlineData(800, "SF12BW125")]
        [InlineData(1023, "SF8BW125")]
        [InlineData(868.1, "SF0BW125")]
        [InlineData(869.3, "SF32BW543")]
        [InlineData(800, "SF0BW125")]
        public void TestLimit(double freq, string datarate)
        {
            TestRegionLimit(freq, datarate);
        }

        [Theory]
        [InlineData("SF12BW125", 59)]
        [InlineData("SF11BW125", 59)]
        [InlineData("SF10BW125", 59)]
        [InlineData("SF9BW125", 123)]
        [InlineData("SF8BW125", 230)]
        [InlineData("SF7BW125", 230)]
        [InlineData("SF7BW250", 230)]
        [InlineData("50", 230)]
        public void TestMaxPayloadLength(string datr, uint maxPyldSize)
        {
            TestRegionMaxPayloadLength(datr, maxPyldSize);
        }

        [Theory]
        [InlineData("", null, null, 869.525, "SF12BW125")] // Standard EU.
        [InlineData("SF9BW125", null, null, 869.525, "SF9BW125")] // nwksrvDR is correctly applied if no device twins.
        [InlineData("SF9BW125", 868.250, (ushort)6, 868.250, "SF7BW250")] // device twins are applied in priority.
        public void TestDownstreamRX2(string nwksrvrx2dr, double? nwksrvrx2freq, ushort? rx2drfromtwins, double expectedFreq, string expectedDr)
        {
            TestDownstreamRX2FrequencyAndDataRate(nwksrvrx2dr, nwksrvrx2freq, rx2drfromtwins, expectedFreq, expectedDr);
        }

        [Fact]
        public void TestTranslateRegionType()
        {
            TestTranslateToRegion(LoRaRegionType.EU868);
        }

        [Fact]
        public void TestResolveRegion()
        {
            TestTryResolveRegion("SF9BW125", 868.1);
        }
    }
}
