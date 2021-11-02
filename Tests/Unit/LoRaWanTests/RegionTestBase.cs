// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using Xunit;

    public class RegionTestBase
    {
        protected Region Region { get; set; }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestRegionFrequencyAndDataRate(string inputDr, double inputFreq, string outputDr, double outputFreq, DeviceJoinInfo deviceJoinInfo = null)
        {
            var rxpk = GenerateRxpk(inputDr, inputFreq);
            TestRegionFrequency(rxpk, outputFreq, deviceJoinInfo);
            TestRegionDataRate(rxpk, outputDr);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestRegionFrequency(IList<Rxpk> rxpk, double outputFreq, DeviceJoinInfo deviceJoinInfo = null)
        {
            Assert.True(Region.TryGetDownstreamChannelFrequency(rxpk[0], out var frequency, deviceJoinInfo));
            Assert.Equal(frequency, outputFreq);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestRegionDataRate(IList<Rxpk> rxpk, string outputDr, int rx1DrOffset = 0)
        {
            Assert.Equal(Region.GetDownstreamDR(rxpk[0], rx1DrOffset), outputDr);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestRegionLimitRxpk(double freq, string datarate, DeviceJoinInfo deviceJoinInfo = null)
        {
            var rxpk = GenerateRxpk(datarate, freq);
            Assert.False(Region.TryGetDownstreamChannelFrequency(rxpk[0], out _, deviceJoinInfo));
            Assert.Null(Region.GetDownstreamDR(rxpk[0]));
        }

        protected void TestRegionFrequencyAndDataRate(ushort inputDataRate,
                                                      double inputFrequency,
                                                      ushort outputDataRate,
                                                      double outputFrequency,
                                                      DeviceJoinInfo deviceJoinInfo = null)
        {
            TestRegionFrequency(inputFrequency, inputDataRate, outputFrequency, deviceJoinInfo);
            TestRegionDataRate(inputFrequency, inputDataRate, outputDataRate);
        }

        protected void TestRegionFrequency(double inputFrequency, ushort inputDataRate, double outputFreq, DeviceJoinInfo deviceJoinInfo = null)
        {
            Assert.True(Region.TryGetDownstreamChannelFrequency(inputFrequency, inputDataRate, out var frequency, deviceJoinInfo));
            Assert.Equal(frequency, outputFreq);
        }

        protected void TestRegionDataRate(double inputFrequency, ushort inputDataRate, ushort outputDr, int rx1DrOffset = 0)
        {
            Assert.Equal(Region.GetDownstreamDR(inputFrequency, inputDataRate, rx1DrOffset), outputDr);
        }

        [Theory]
        [MemberData(nameof(RegionEU868Test.TestRegionLimitData), MemberType = typeof(RegionEU868Test))]
        [MemberData(nameof(RegionUS915Test.TestRegionLimitData), MemberType = typeof(RegionUS915Test))]
        [MemberData(nameof(RegionCN470Test.TestRegionLimitData), MemberType = typeof(RegionCN470Test))]
        public void TestRegionLimit(Region region, double inputFrequency, ushort datarate, int? reportedJoinChannel = null)
        {
            var deviceJoinInfo = new DeviceJoinInfo(reportedJoinChannel);
            Assert.False(region.TryGetDownstreamChannelFrequency(inputFrequency, datarate, out _, deviceJoinInfo));
            Assert.Null(region.GetDownstreamDR(inputFrequency, datarate));
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

        [Theory]
#pragma warning disable CS0618 // Type or member is obsolete; classes will be deleted as soon as the complete LNS implementation is done
        [MemberData(nameof(RegionEU868TestWithRxpk.TestRegionMaxPayloadLengthData), MemberType = typeof(RegionEU868TestWithRxpk))]
        [MemberData(nameof(RegionUS915TestWithRxpk.TestRegionMaxPayloadLengthData), MemberType = typeof(RegionUS915TestWithRxpk))]
        [MemberData(nameof(RegionCN470TestWithRxpk.TestRegionMaxPayloadLengthData), MemberType = typeof(RegionCN470TestWithRxpk))]
#pragma warning restore CS0618 // Type or member is obsolete
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        public void TestRegionMaxPayloadLengthRxpk(Region region, string datarate, uint maxPyldSize)
        {
            Assert.Equal(region.GetMaxPayloadSize(datarate), maxPyldSize);
        }

        [Theory]
        [MemberData(nameof(RegionEU868Test.TestRegionMaxPayloadLengthData), MemberType = typeof(RegionEU868Test))]
        [MemberData(nameof(RegionUS915Test.TestRegionMaxPayloadLengthData), MemberType = typeof(RegionUS915Test))]
        [MemberData(nameof(RegionCN470Test.TestRegionMaxPayloadLengthData), MemberType = typeof(RegionCN470Test))]
        public void TestRegionMaxPayloadLength(Region region, ushort datarate, uint maxPyldSize)
        {
            Assert.Equal(region.GetMaxPayloadSize(datarate), maxPyldSize);
        }

        [Theory]
        [MemberData(nameof(RegionEU868Test.TestDownstreamRX2FrequencyData), MemberType = typeof(RegionEU868Test))]
        [MemberData(nameof(RegionUS915Test.TestDownstreamRX2FrequencyData), MemberType = typeof(RegionUS915Test))]
        [MemberData(nameof(RegionCN470Test.TestDownstreamRX2FrequencyData), MemberType = typeof(RegionCN470Test))]
        public void TestDownstreamRX2Frequency(double? nwksrvrx2freq, double expectedFreq, int? reportedJoinChannel = null, int? desiredJoinChannel = null)
        {
            var devEui = "testDevice";
            var deviceJoinInfo = new DeviceJoinInfo(reportedJoinChannel, desiredJoinChannel);
            var freq = Region.GetDownstreamRX2Freq(devEui, nwksrvrx2freq, deviceJoinInfo);
            Assert.Equal(expectedFreq, freq);
        }

        [Theory]
#pragma warning disable CS0618 // Type or member is obsolete; classes will be deleted as soon as the complete LNS implementation is done
        [MemberData(nameof(RegionEU868TestWithRxpk.TestDownstreamRX2DataRateRxpkData), MemberType = typeof(RegionEU868TestWithRxpk))]
        [MemberData(nameof(RegionUS915TestWithRxpk.TestDownstreamRX2DataRateRxpkData), MemberType = typeof(RegionUS915TestWithRxpk))]
        [MemberData(nameof(RegionCN470TestWithRxpk.TestDownstreamRX2DataRateRxpkData), MemberType = typeof(RegionCN470TestWithRxpk))]
#pragma warning restore CS0618 // Type or member is obsolete
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        public void TestDownstreamRX2DataRateRxpk(Region region, string nwksrvrx2dr, ushort? rx2drfromtwins, string expectedDr, int? reportedJoinChannel = null, int? desiredJoinChannel = null)
        {
            var devEui = "testDevice";
            var deviceJoinInfo = new DeviceJoinInfo(reportedJoinChannel, desiredJoinChannel);
            var datr = region.GetDownstreamRX2Datarate(devEui, nwksrvrx2dr, rx2drfromtwins, deviceJoinInfo);
            Assert.Equal(expectedDr, datr);
        }

        [Theory]
        [MemberData(nameof(RegionEU868Test.TestDownstreamRX2DataRateData), MemberType = typeof(RegionEU868Test))]
        [MemberData(nameof(RegionUS915Test.TestDownstreamRX2DataRateData), MemberType = typeof(RegionUS915Test))]
        [MemberData(nameof(RegionCN470Test.TestDownstreamRX2DataRateData), MemberType = typeof(RegionCN470Test))]
        public void TestDownstreamRX2DataRate(Region region, ushort? nwksrvrx2dr, ushort? rx2drfromtwins, ushort expectedDr, int? reportedJoinChannel = null, int? desiredJoinChannel = null)
        {
            var devEui = "testDevice";
            var deviceJoinInfo = new DeviceJoinInfo(reportedJoinChannel, desiredJoinChannel);
            var datr = region.GetDownstreamRX2Datarate(devEui, nwksrvrx2dr, rx2drfromtwins, deviceJoinInfo);
            Assert.Equal(expectedDr, datr);
        }

        [Theory]
        [MemberData(nameof(RegionUS915Test.TestTranslateToRegionData), MemberType = typeof(RegionUS915Test))]
        [MemberData(nameof(RegionEU868Test.TestTranslateToRegionData), MemberType = typeof(RegionEU868Test))]
        [MemberData(nameof(RegionCN470Test.TestTranslateToRegionData), MemberType = typeof(RegionCN470Test))]
        public void TestTranslateToRegion(Region region, LoRaRegionType loRaRegion)
        {
            Assert.True(RegionManager.TryTranslateToRegion(loRaRegion, out var translatedRegion));
            Assert.IsType(region.GetType(), translatedRegion);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestTryResolveRegionRxpk(string dr, double freq)
        {
            var rxpk = GenerateRxpk(dr, freq);
            Assert.True(RegionManager.TryResolveRegion(rxpk[0], out var region));
            Assert.IsType(Region.GetType(), region);
        }

        [Theory]
        [MemberData(nameof(RegionUS915Test.TestTryGetJoinChannelIndexData), MemberType = typeof(RegionUS915Test))]
        [MemberData(nameof(RegionEU868Test.TestTryGetJoinChannelIndexData), MemberType = typeof(RegionEU868Test))]
        [MemberData(nameof(RegionCN470Test.TestTryGetJoinChannelIndexData), MemberType = typeof(RegionCN470Test))]
        public void TestTryGetJoinChannelIndex(Region region, double freq, int expectedIndex)
        {
            Assert.Equal(expectedIndex != -1, region.TryGetJoinChannelIndex(freq, out var channelIndex));
            Assert.Equal(expectedIndex, channelIndex);
        }

        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        protected void TestTryGetJoinChannelIndexRxpk(string dr, double freq, int expectedIndex)
        {
            var rxpk = GenerateRxpk(dr, freq);
            Assert.False(Region.TryGetJoinChannelIndex(rxpk[0], out var channelIndex));
            Assert.Equal(expectedIndex, channelIndex);
        }

        [Theory]
        [MemberData(nameof(RegionCN470Test.TestIsValidRX1DROffsetData), MemberType = typeof(RegionCN470Test))]
        [MemberData(nameof(RegionEU868Test.TestIsValidRX1DROffsetData), MemberType = typeof(RegionEU868Test))]
        [MemberData(nameof(RegionUS915Test.TestIsValidRX1DROffsetData), MemberType = typeof(RegionUS915Test))]
        public void TestIsValidRX1DROffset(Region region, uint offset, bool isValid)
        {
            Assert.Equal(isValid, region.IsValidRX1DROffset(offset));
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(15, true)]
        [InlineData(16, false)]
        [InlineData(50, false)]
        public void TestIsValidRXDelay(ushort delay, bool isValid)
        {
            Assert.Equal(isValid, Region.IsValidRXDelay(delay));
        }

        [Theory]
        [MemberData(nameof(RegionEU868Test.TestIsDRIndexWithinAcceptableValuesData), MemberType = typeof(RegionEU868Test))]
        [MemberData(nameof(RegionUS915Test.TestIsDRIndexWithinAcceptableValuesData), MemberType = typeof(RegionUS915Test))]
        [MemberData(nameof(RegionCN470Test.TestIsDRIndexWithinAcceptableValuesData), MemberType = typeof(RegionCN470Test))]
        public void TestIsDRIndexWithinAcceptableValues(Region region, ushort? datarate, bool upstream, bool isValid)
        {
            if (upstream && datarate != null)
            {
                Assert.Equal(isValid, region.RegionLimits.IsCurrentUpstreamDRIndexWithinAcceptableValue((ushort)datarate));
            }
            else
            {
                Assert.Equal(isValid, region.RegionLimits.IsCurrentDownstreamDRIndexWithinAcceptableValue(datarate));
            }
        }
    }
}
