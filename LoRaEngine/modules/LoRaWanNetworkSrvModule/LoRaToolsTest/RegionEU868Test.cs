namespace LoRaWanTest
{
    using System.Collections.Generic;
    using LoRaTools.Regions;
    using Xunit;

    public class RegionEU868Test : RegionTestBase
    {
        public RegionEU868Test()
        {
            _region = RegionManager.EU868;
        }

        [Theory]
        [MemberData(nameof(CreateValidTestData))]
        public void TestFrequencyAndDataRate(string inputDr, double inputFreq, string outputDr, double outputFreq)
        {
            TestRegionFrequencyAndDataRate(inputDr, inputFreq, outputDr, outputFreq);
        }

        public static IEnumerable<object[]> CreateValidTestData()
        {
            var dataRates = new string[] { "SF12BW125", "SF11BW125", "SF10BW125", "SF9BW125", "SF8BW125", "SF7BW125", "SF7BW250" };
            var frequencies = new double[] { 868.1, 868.3, 868.5 };

            foreach (var dr in dataRates)
                foreach (var freq in frequencies)
                    yield return new object[] { dr, freq, dr, freq };  
        }

        [Theory]
        [InlineData(800, "SF12BW125")]
        [InlineData(1023, "SF8BW125")]
        [InlineData(868.1, "SF0BW125")]
        [InlineData(869.3, "SF32BW543")]
        [InlineData(800, "SF0BW125")]
        public void TestRegionLimit(double freq, string datarate)
        {
            var rxpk = GenerateRxpk(datarate, freq);
            Assert.False(_region.TryGetDownstreamChannelFrequency(rxpk[0], out _) &&
                _region.GetDownstreamDR(rxpk[0]) != null);
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
            Assert.Equal(_region.GetMaxPayloadSize(datr), maxPyldSize);
        }

        [Theory]
        [InlineData("", null, null, 869.525, "SF12BW125")] // Standard EU.
        [InlineData("SF9BW125", null, null, 869.525, "SF9BW125")] // nwksrvDR is correctly applied if no device twins.
        [InlineData("SF9BW125", 868.250, (ushort)6, 868.250, "SF7BW250")] // device twins are applied in priority.
        public void TestDownstreamRX2(string nwksrvrx2dr, double? nwksrvrx2freq, ushort? rx2drfromtwins, double expectedFreq, string expectedDr)
        {
            TestDownstreamRX2FrequencyAndDataRate(nwksrvrx2dr, nwksrvrx2freq, rx2drfromtwins, expectedFreq, expectedDr);
        }
    }
}
