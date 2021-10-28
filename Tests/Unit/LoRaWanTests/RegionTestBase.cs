namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using Xunit;

    public abstract class RegionTestBase
    {
        protected Region Region { get; set; }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestRegionFrequencyAndDataRate(string inputDr, double inputFreq, string outputDr, double outputFreq, int? joinChannelIndex = null)
        {
            var rxpk = GenerateRxpk(inputDr, inputFreq);
            TestRegionFrequency(rxpk, outputFreq, joinChannelIndex);
            TestRegionDataRate(rxpk, outputDr);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestRegionFrequency(IList<Rxpk> rxpk, double outputFreq, int? joinChannelIndex = null)
        {
            Assert.True(Region.TryGetDownstreamChannelFrequency(rxpk[0], out var frequency, joinChannelIndex));
            Assert.Equal(frequency, outputFreq);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestRegionDataRate(IList<Rxpk> rxpk, string outputDr, int rx1DrOffset = 0)
        {
            Assert.Equal(Region.GetDownstreamDR(rxpk[0], rx1DrOffset), outputDr);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestRegionLimit(double freq, string datarate, int? joinChannelIndex = null)
        {
            var rxpk = GenerateRxpk(datarate, freq);
            Assert.False(Region.TryGetDownstreamChannelFrequency(rxpk[0], out _, joinChannelIndex));
            Assert.Null(Region.GetDownstreamDR(rxpk[0]));
        }

        protected void TestRegionFrequencyAndDataRate(ushort inputDataRate,
                                                      double inputFrequency,
                                                      ushort outputDataRate,
                                                      double outputFrequency,
                                                      int? joinChannelIndex = null)
        {
            TestRegionFrequency(inputFrequency, inputDataRate, outputFrequency, joinChannelIndex);
            TestRegionDataRate(inputFrequency, inputDataRate, outputDataRate);
        }

        protected void TestRegionFrequency(double inputFrequency, ushort inputDataRate, double outputFreq, int? joinChannelIndex = null)
        {
            Assert.True(Region.TryGetDownstreamChannelFrequency(inputFrequency, inputDataRate, out var frequency, joinChannelIndex));
            Assert.Equal(frequency, outputFreq);
        }

        protected void TestRegionDataRate(double inputFrequency, ushort inputDataRate, ushort outputDr, int rx1DrOffset = 0)
        {
            Assert.Equal(Region.GetDownstreamDR(inputFrequency, inputDataRate, rx1DrOffset), outputDr);
        }

        protected void TestRegionLimit(double inputFrequency, ushort datarate, int? joinChannelIndex = null)
        {
            Assert.False(Region.TryGetDownstreamChannelFrequency(inputFrequency, datarate, out _, joinChannelIndex));
            Assert.Null(Region.GetDownstreamDR(inputFrequency, datarate));
        }

        protected static IList<Rxpk> GenerateRxpk(string dr, double freq)
        {
            var jsonUplink =
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
            var physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            return Rxpk.CreateRxpk(physicalUpstreamPyld.Concat(multiRxpkInput).ToArray());
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestRegionMaxPayloadLength(string datr, uint maxPyldSize)
        {
            Assert.Equal(Region.GetMaxPayloadSize(datr), maxPyldSize);
        }

        protected void TestRegionMaxPayloadLength(ushort datarate, uint maxPyldSize)
        {
            Assert.Equal(Region.GetMaxPayloadSize(datarate), maxPyldSize);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestDownstreamRX2FrequencyAndDataRate(string nwksrvrx2dr, double? nwksrvrx2freq, ushort? rx2drfromtwins, double expectedFreq, string expectedDr)
        {
            var devEui = "testDevice";
            var datr = Region.GetDownstreamRX2Datarate(devEui, nwksrvrx2dr, rx2drfromtwins);
            var freq = Region.GetDownstreamRX2Freq(devEui, nwksrvrx2freq);
            Assert.Equal(expectedFreq, freq);
            Assert.Equal(expectedDr, datr);
        }

        protected void TestDownstreamRX2FrequencyAndDataRate(ushort? nwksrvrx2dr, double? nwksrvrx2freq, ushort? rx2drfromtwins, double expectedFreq, ushort expectedDr)
        {
            var devEui = "testDevice";
            var datr = Region.GetDownstreamRX2Datarate(devEui, nwksrvrx2dr, rx2drfromtwins);
            var freq = Region.GetDownstreamRX2Freq(devEui, nwksrvrx2freq);
            Assert.Equal(expectedFreq, freq);
            Assert.Equal(expectedDr, datr);
        }

        protected void TestTranslateToRegion(LoRaRegionType loRaRegion)
        {
            Assert.True(RegionManager.TryTranslateToRegion(loRaRegion, out var region));
            Assert.IsType(Region.GetType(), region);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestTryResolveRegionRxpk(string dr, double freq)
        {
            var rxpk = GenerateRxpk(dr, freq);
            Assert.True(RegionManager.TryResolveRegion(rxpk[0], out var region));
            Assert.IsType(Region.GetType(), region);
        }

        protected void TestTryResolveRegion(double freq)
        {
            Assert.True(RegionManager.TryResolveRegion(freq, out var region));
            Assert.IsType(Region.GetType(), region);
        }
    }
}
