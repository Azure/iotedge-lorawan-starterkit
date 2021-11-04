// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using Xunit;

    public class RegionTest
    {
        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestRegionFrequencyData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestRegionFrequencyDataDR1To3), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionUS915TestData.TestRegionFrequencyDataDR4), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470TestData.TestRegionFrequencyData), MemberType = typeof(RegionCN470TestData))]
        public void TestDownstreamFrequency(Region region, double inputFrequency, ushort inputDataRate, double outputFreq, int? joinChannel = null)
        {
            var deviceJoinInfo = new DeviceJoinInfo(joinChannel);
            Assert.True(region.TryGetDownstreamChannelFrequency(inputFrequency, inputDataRate, out var frequency, deviceJoinInfo));
            Assert.Equal(frequency, outputFreq);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestRegionDataRateData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestRegionDataRateDataDR1To3), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionUS915TestData.TestRegionDataRateDataDR4), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470TestData.TestRegionDataRateData), MemberType = typeof(RegionCN470TestData))]
        public void TestDownstreamDataRate(Region region, double inputFrequency, ushort inputDataRate, ushort outputDr, int rx1DrOffset = 0)
        {
            Assert.Equal(region.GetDownstreamDataRate(inputFrequency, inputDataRate, rx1DrOffset), outputDr);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestRegionLimitData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestRegionLimitData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470TestData.TestRegionLimitData), MemberType = typeof(RegionCN470TestData))]
        public void TestRegionLimits(Region region, double inputFrequency, ushort datarate, int? joinChannel = null)
        {
            var deviceJoinInfo = new DeviceJoinInfo(joinChannel);
            Assert.False(region.TryGetDownstreamChannelFrequency(inputFrequency, datarate, out _, deviceJoinInfo));
            Assert.Null(region.GetDownstreamDataRate(inputFrequency, datarate));
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestRegionMaxPayloadLengthData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestRegionMaxPayloadLengthData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470TestData.TestRegionMaxPayloadLengthData), MemberType = typeof(RegionCN470TestData))]
        public void TestMaxPayloadLength(Region region, ushort datarate, uint maxPyldSize)
        {
            Assert.Equal(region.GetMaxPayloadSize(datarate), maxPyldSize);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestDownstreamRX2FrequencyData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestDownstreamRX2FrequencyData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470TestData.TestDownstreamRX2FrequencyData), MemberType = typeof(RegionCN470TestData))]
        public void TestDownstreamRX2Frequency(Region region, double? nwksrvrx2freq, double expectedFreq, int? reportedJoinChannel = null, int? desiredJoinChannel = null)
        {
            var devEui = "testDevice";
            var deviceJoinInfo = new DeviceJoinInfo(reportedJoinChannel, desiredJoinChannel);
            var freq = region.GetDownstreamRX2Freq(devEui, nwksrvrx2freq, deviceJoinInfo);
            Assert.Equal(expectedFreq, freq);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestDownstreamRX2DataRateData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestDownstreamRX2DataRateData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470TestData.TestDownstreamRX2DataRateData), MemberType = typeof(RegionCN470TestData))]
        public void TestDownstreamRX2DataRate(Region region, ushort? nwksrvrx2dr, ushort? rx2drfromtwins, ushort expectedDr, int? reportedJoinChannel = null, int? desiredJoinChannel = null)
        {
            var devEui = "testDevice";
            var deviceJoinInfo = new DeviceJoinInfo(reportedJoinChannel, desiredJoinChannel);
            var datr = region.GetDownstreamRX2Datarate(devEui, nwksrvrx2dr, rx2drfromtwins, deviceJoinInfo);
            Assert.Equal(expectedDr, datr);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestTranslateToRegionData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestTranslateToRegionData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470TestData.TestTranslateToRegionData), MemberType = typeof(RegionCN470TestData))]
        public void TestTranslateToRegion(Region region, LoRaRegionType loRaRegion)
        {
            Assert.True(RegionManager.TryTranslateToRegion(loRaRegion, out var translatedRegion));
            Assert.IsType(region.GetType(), translatedRegion);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestTryGetJoinChannelIndexData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestTryGetJoinChannelIndexData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470TestData.TestTryGetJoinChannelIndexData), MemberType = typeof(RegionCN470TestData))]
        public void TestTryGetJoinChannelIndex(Region region, double freq, int expectedIndex)
        {
            Assert.Equal(expectedIndex != -1, region.TryGetJoinChannelIndex(freq, out var channelIndex));
            Assert.Equal(expectedIndex, channelIndex);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestIsValidRX1DROffsetData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestIsValidRX1DROffsetData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470TestData.TestIsValidRX1DROffsetData), MemberType = typeof(RegionCN470TestData))]
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
        [MemberData(nameof(RegionEU868TestData.TestIsDRIndexWithinAcceptableValuesData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestIsDRIndexWithinAcceptableValuesData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470TestData.TestIsDRIndexWithinAcceptableValuesData), MemberType = typeof(RegionCN470TestData))]
        public void TestIsDRIndexWithinAcceptableValues(Region region, ushort? datarate, bool upstream, bool isValid)
        {
            if (upstream)
            {
                Assert.NotNull(datarate);
                Assert.Equal(isValid, region.RegionLimits.IsCurrentUpstreamDRIndexWithinAcceptableValue((ushort)datarate));
            }
            else
            {
                Assert.Equal(isValid, region.RegionLimits.IsCurrentDownstreamDRIndexWithinAcceptableValue(datarate));
            }
        }

        private static IList<Rxpk> GenerateRxpk(string dr, double freq)
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
    }
}
