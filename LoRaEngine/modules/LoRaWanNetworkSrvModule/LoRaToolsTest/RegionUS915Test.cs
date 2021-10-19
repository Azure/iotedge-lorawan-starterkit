namespace LoRaWanTest
{
    using System.Collections.Generic;
    using LoRaTools.Regions;
    using Xunit;

    public class RegionUS915Test : RegionTestBase
    {
        public RegionUS915Test()
        {
            Region = RegionManager.US915;
        }

        [Theory]
        [PairwiseData]
        public void TestFrequencyAndDataRateDR1To3(
            [CombinatorialValues("SF10BW125", "SF9BW125", "SF8BW125", "SF7BW125")] string inputDr,
            [CombinatorialValues(902.3, 902.5, 902.7, 902.9, 903.1, 903.3, 903.5, 903.7, 903.9)] double inputFreq)
        {
            var inputDrToExpectedDr = new Dictionary<string, string>
            {
                {"SF10BW125", "SF10BW500" },
                {"SF9BW125", "SF9BW500" },
                {"SF8BW125", "SF8BW500" },
                {"SF7BW125", "SF7BW500" }
            };

            var inputFreqToExpectedFreq = new Dictionary<double, double>
            {
                { 902.3, 923.3 },
                { 902.5, 923.9 },
                { 902.7, 924.5 },
                { 902.9, 925.1 },
                { 903.1, 925.7 },
                { 903.3, 926.3 },
                { 903.5, 926.9 },
                { 903.7, 927.5 },
                { 903.9, 923.3 },
                { 904.1, 923.9 },
                { 904.3, 924.5 },
            };

            var expectedDr = inputDrToExpectedDr[inputDr];
            var expectedFreq = inputFreqToExpectedFreq[inputFreq];

            TestRegionFrequencyAndDataRate(inputDr, inputFreq, expectedDr, expectedFreq);
        }

        [Theory]
        [PairwiseData]
        public void TestFrequencyAndDataRateDR4(
            [CombinatorialValues("SF8BW500")] string inputDr,
            [CombinatorialValues(903, 904.6, 906.2, 907.8, 909.4, 911, 912.6, 914.2)] double inputFreq)
        {
            var expectedDr = "SF7BW500";

            var inputFreqToExpectedFreq = new Dictionary<double, double>
            {
                { 903,   923.3 },
                { 904.6, 923.9 },
                { 906.2, 924.5 },
                { 907.8, 925.1 },
                { 909.4, 925.7 },
                { 911,   926.3 },
                { 912.6, 926.9 },
                { 914.2, 927.5 },
            };

            var expectedFreq = inputFreqToExpectedFreq[inputFreq];

            TestRegionFrequencyAndDataRate(inputDr, inputFreq, expectedDr, expectedFreq);
        }

        [Theory]
        [InlineData(700, "SF10BW125")]
        [InlineData(1024, "SF8BW125")]
        [InlineData(915, "SF0BW125")]
        [InlineData(920, "SF30BW400")]
        public void TestLimit(double freq, string datarate)
        {
            TestRegionLimit(freq, datarate);
        }

        [Theory]
        [InlineData("SF10BW125", 19)]
        [InlineData("SF9BW125", 61)]
        [InlineData("SF8BW125", 133)]
        [InlineData("SF7BW125", 250)]
        [InlineData("SF8BW500", 250)]
        [InlineData("SF12BW500", 61)]
        [InlineData("SF11BW500", 137)]
        [InlineData("SF10BW500", 250)]
        [InlineData("SF9BW500", 250)]
        [InlineData("SF7BW500", 250)]
        public void TestMaxPayloadLength(string datr, uint maxPyldSize)
        {
            TestRegionMaxPayloadLength(datr, maxPyldSize);
        }

        [Theory]
        [InlineData("", null, null, 923.3, "SF12BW500")] // Standard US.
        [InlineData("SF9BW500", null, null, 923.3, "SF9BW500")] // Standard EU.
        [InlineData("SF9BW500", 920.0, (ushort)12, 920.0, "SF8BW500")] // Standard EU.
        public void TestDownstreamRX2(string nwksrvrx2dr, double? nwksrvrx2freq, ushort? rx2drfromtwins, double expectedFreq, string expectedDr)
        {
            TestDownstreamRX2FrequencyAndDataRate(nwksrvrx2dr, nwksrvrx2freq, rx2drfromtwins, expectedFreq, expectedDr);
        }

        [Fact]
        public void TestTranslateRegionType()
        {
            TestTranslateToRegion(LoRaRegionType.US915);
        }

        [Fact]
        public void TestResolveRegion()
        {
            TestTryResolveRegion("SF9BW500", 902.3);
        }
    }
}
