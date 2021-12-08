// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using global::LoRaTools.LoRaPhysical;
    using global::LoRaTools.Regions;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;
    using static LoRaWan.Metric;

    public abstract class RegionTestBaseRxpk
    {
        protected Region Region { get; set; }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestRegionFrequencyAndDataRate(string inputDr, double inputFreq, string outputDr, double outputFreq, DeviceJoinInfo deviceJoinInfo = null)
        {
            var rxpk = GenerateRxpk(inputDr, inputFreq);
            TestRegionFrequencyRxpk(rxpk, outputFreq, deviceJoinInfo);
            TestRegionDataRateRxpk(rxpk, outputDr);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestRegionFrequencyRxpk(IList<Rxpk> rxpk, double outputFreq, DeviceJoinInfo deviceJoinInfo = null)
        {
            Assert.True(Region.TryGetDownstreamChannelFrequency(rxpk[0], out var frequency, deviceJoinInfo));
            Assert.Equal(frequency, outputFreq);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestRegionDataRateRxpk(IList<Rxpk> rxpk, string outputDr, int rx1DrOffset = 0)
        {
            Assert.Equal(Region.GetDownstreamDataRate(rxpk[0], rx1DrOffset), outputDr);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestRegionDataRateRxpk_ThrowsWhenOffsetInvalid(string inputDr, double inputFreq, int rx1DrOffset)
        {
            var rxpk = GenerateRxpk(inputDr, inputFreq);
            var ex = Assert.Throws<LoRaProcessingException>(() => Region.GetDownstreamDataRate(rxpk[0], rx1DrOffset));
            Assert.Equal(LoRaProcessingErrorCode.InvalidDataRateOffset, ex.ErrorCode);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestRegionLimitRxpk(double freq, string datarate, DeviceJoinInfo deviceJoinInfo = null)
        {
            var rxpk = GenerateRxpk(datarate, freq);
            var ex = Assert.Throws<LoRaProcessingException>(() => Region.TryGetDownstreamChannelFrequency(rxpk[0], out _, deviceJoinInfo));
            Assert.Contains("Invalid upstream channel", ex.Message, StringComparison.InvariantCulture);
            ex = Assert.Throws<LoRaProcessingException>(() => Region.GetDownstreamDataRate(rxpk[0]));
            Assert.Contains("Invalid upstream channel", ex.Message, StringComparison.InvariantCulture);
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
        protected void TestRegionMaxPayloadLength(string datarate, uint maxPyldSize)
        {
            Assert.Equal(Region.GetMaxPayloadSize(datarate), maxPyldSize);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestDownstreamRX2FrequencyAndDataRate(string nwksrvrx2dr, double? nwksrvrx2freq, ushort? rx2drfromtwins,
            double expectedFreq, string expectedDr, DeviceJoinInfo deviceJoinInfo = null)
        {
            TestDownstreamRX2Frequency(nwksrvrx2freq is { } someFreq ? Mega(someFreq) : null, Mega(expectedFreq), deviceJoinInfo);
            TestDownstreamRX2DataRate(nwksrvrx2dr, rx2drfromtwins, expectedDr);
        }

        protected void TestDownstreamRX2Frequency(Hertz? nwksrvrx2freq, Hertz expectedFreq, DeviceJoinInfo deviceJoinInfo = null)
        {
            var freq = Region.GetDownstreamRX2Freq(nwksrvrx2freq, NullLogger.Instance, deviceJoinInfo);
            Assert.Equal(expectedFreq, freq);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestDownstreamRX2DataRate(string nwksrvrx2dr, ushort? rx2drfromtwins, string expectedDr, DeviceJoinInfo deviceJoinInfo = null)
        {
            var devEui = "testDevice";
            var datr = Region.GetDownstreamRX2DataRate(devEui, nwksrvrx2dr, rx2drfromtwins, NullLogger.Instance, deviceJoinInfo);
            Assert.Equal(expectedDr, datr);
        }


        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestTryResolveRegionRxpk(string dr, double freq)
        {
            var rxpk = GenerateRxpk(dr, freq);
            Assert.True(RegionManager.TryResolveRegion(rxpk[0], out var region));
            Assert.IsType(Region.GetType(), region);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestTryGetJoinChannelIndexRxpk(string dr, double freq, int expectedIndex)
        {
            var rxpk = GenerateRxpk(dr, freq);
            Assert.False(Region.TryGetJoinChannelIndex(rxpk[0], out var channelIndex));
            Assert.Equal(expectedIndex, channelIndex);
        }
    }
}
