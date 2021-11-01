namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using LoRaWan.NetworkServer;
    using Xunit;

    public sealed class ConcentratorDeduplicationTest : System.IDisposable
    {
        private readonly ConcentratorDeduplication _concentratorDeduplication;

        public ConcentratorDeduplicationTest()
        {
            this._concentratorDeduplication = new ConcentratorDeduplication();
        }

        [Fact]
        public void EmptyCache()
        {
            var loraRequest = new LoRaRequest(default, default, default, "lbsa");
            var result = _concentratorDeduplication.IsDuplicate(loraRequest, 1, "devicea");
            Assert.False(result);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, false)]
        [InlineData(2, false)]
        public void NonEmptyCacheFromSameConcentrator(uint frameCounter, bool expectedResult)
        {
            var loraRequest = new LoRaRequest(default, default, default, "lbsa");
            var deviceEUI = "devicea";
            _ = _concentratorDeduplication.IsDuplicate(loraRequest, 1, deviceEUI); // add an item 

            var result = _concentratorDeduplication.IsDuplicate(loraRequest, frameCounter, deviceEUI);
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        public void NonEmptyCacheFromDifferentConcentrator(uint frameCounter, bool expectedResult)
        {
            var loraRequest = new LoRaRequest(default, default, default, "lbsa");
            var deviceEUI = "devicea";
            _ = _concentratorDeduplication.IsDuplicate(loraRequest, 1, deviceEUI); // add an item 

            var loraRequest2 = new LoRaRequest(default, default, default, "differentLBS");
            var result = _concentratorDeduplication.IsDuplicate(loraRequest2, frameCounter, deviceEUI);
            Assert.Equal(expectedResult, result);
        }

        public void Dispose() => this._concentratorDeduplication.Dispose();
    }
}
