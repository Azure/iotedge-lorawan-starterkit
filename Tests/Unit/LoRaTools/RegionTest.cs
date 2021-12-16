// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaTools.Regions
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using global::LoRaTools.LoRaPhysical;
    using global::LoRaTools.Regions;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class RegionTest
    {
        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestRegionFrequencyData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestRegionFrequencyDataDR1To3), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionUS915TestData.TestRegionFrequencyDataDR4), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470RP1TestData.TestRegionFrequencyData), MemberType = typeof(RegionCN470RP1TestData))]
        [MemberData(nameof(RegionCN470RP2TestData.TestRegionFrequencyData), MemberType = typeof(RegionCN470RP2TestData))]
        [MemberData(nameof(RegionAS923TestData.TestRegionFrequencyData), MemberType = typeof(RegionAS923TestData))]
        public void TestDownstreamFrequency(Region region, Hertz inputFrequency, ushort inputDataRate, Hertz outputFreq, int? joinChannel = null)
        {
            var deviceJoinInfo = new DeviceJoinInfo(joinChannel);
            Assert.True(region.TryGetDownstreamChannelFrequency(inputFrequency, out var frequency, inputDataRate, deviceJoinInfo));
            Assert.Equal(frequency, outputFreq);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestRegionDataRateData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestRegionDataRateDataDR1To3), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionUS915TestData.TestRegionDataRateDataDR4), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470RP1TestData.TestRegionDataRateData), MemberType = typeof(RegionCN470RP1TestData))]
        [MemberData(nameof(RegionCN470RP2TestData.TestRegionDataRateData), MemberType = typeof(RegionCN470RP2TestData))]
        [MemberData(nameof(RegionAS923TestData.TestRegionDataRateData), MemberType = typeof(RegionAS923TestData))]
        public void TestDownstreamDataRate(Region region, ushort inputDataRate, ushort outputDr, int rx1DrOffset = 0)
        {
            Assert.Equal(region.GetDownstreamDataRate(inputDataRate, rx1DrOffset), outputDr);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestRegionDataRateData_InvalidOffset), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestRegionDataRateData_InvalidOffset), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionAS923TestData.TestRegionDataRateData_InvalidOffset), MemberType = typeof(RegionAS923TestData))]
        [MemberData(nameof(RegionCN470RP1TestData.TestRegionDataRateData_InvalidOffset), MemberType = typeof(RegionCN470RP1TestData))]
        [MemberData(nameof(RegionCN470RP2TestData.TestRegionDataRateData_InvalidOffset), MemberType = typeof(RegionCN470RP2TestData))]
        public void GetDownstreamDataRate_ThrowsWhenOffsetInvalid(Region region, ushort inputDataRate, int rx1DrOffset)
        {
            var ex = Assert.Throws<LoRaProcessingException>(() => region.GetDownstreamDataRate(inputDataRate, rx1DrOffset));
            Assert.Equal(LoRaProcessingErrorCode.InvalidDataRateOffset, ex.ErrorCode);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestRegionLimitData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestRegionLimitData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470RP1TestData.TestRegionLimitData), MemberType = typeof(RegionCN470RP1TestData))]
        [MemberData(nameof(RegionCN470RP2TestData.TestRegionLimitData), MemberType = typeof(RegionCN470RP2TestData))]
        [MemberData(nameof(RegionAS923TestData.TestRegionLimitData), MemberType = typeof(RegionAS923TestData))]
        public void TestRegionLimit(Region region, Hertz inputFrequency, ushort datarate, int? joinChannel = null)
        {
            var deviceJoinInfo = new DeviceJoinInfo(joinChannel);
            var ex = Assert.Throws<LoRaProcessingException>(() => region.TryGetDownstreamChannelFrequency(inputFrequency, out _, datarate, deviceJoinInfo));
            Assert.Equal(LoRaProcessingErrorCode.InvalidFrequency, ex.ErrorCode);
             ex = Assert.Throws<LoRaProcessingException>(() => region.GetDownstreamDataRate(datarate));
            Assert.Equal(LoRaProcessingErrorCode.InvalidDataRate, ex.ErrorCode);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestRegionMaxPayloadLengthData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestRegionMaxPayloadLengthData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470RP1TestData.TestRegionMaxPayloadLengthData), MemberType = typeof(RegionCN470RP1TestData))]
        [MemberData(nameof(RegionCN470RP2TestData.TestRegionMaxPayloadLengthData), MemberType = typeof(RegionCN470RP2TestData))]
        [MemberData(nameof(RegionAS923TestData.TestRegionMaxPayloadLengthData), MemberType = typeof(RegionAS923TestData))]
        public void TestMaxPayloadLength(Region region, ushort datarate, uint maxPyldSize)
        {
            Assert.Equal(region.GetMaxPayloadSize(datarate), maxPyldSize);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestDownstreamRX2FrequencyData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestDownstreamRX2FrequencyData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470RP1TestData.TestDownstreamRX2FrequencyData), MemberType = typeof(RegionCN470RP1TestData))]
        [MemberData(nameof(RegionCN470RP2TestData.TestDownstreamRX2FrequencyData), MemberType = typeof(RegionCN470RP2TestData))]
        [MemberData(nameof(RegionAS923TestData.TestDownstreamRX2FrequencyData), MemberType = typeof(RegionAS923TestData))]
        public void TestDownstreamRX2Frequency(Region region, Hertz? nwksrvrx2freq, Hertz expectedFreq, int? reportedJoinChannel = null, int? desiredJoinChannel = null)
        {
            var deviceJoinInfo = new DeviceJoinInfo(reportedJoinChannel, desiredJoinChannel);
            var freq = region.GetDownstreamRX2Freq(nwksrvrx2freq, NullLogger.Instance, deviceJoinInfo);
            Assert.Equal(expectedFreq, freq);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestDownstreamRX2DataRateData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestDownstreamRX2DataRateData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470RP1TestData.TestDownstreamRX2DataRateData), MemberType = typeof(RegionCN470RP1TestData))]
        [MemberData(nameof(RegionCN470RP2TestData.TestDownstreamRX2DataRateData), MemberType = typeof(RegionCN470RP2TestData))]
        [MemberData(nameof(RegionAS923TestData.TestDownstreamRX2DataRateData), MemberType = typeof(RegionAS923TestData))]
        public void TestDownstreamRX2DataRate(Region region, ushort? nwksrvrx2dr, ushort? rx2drfromtwins, ushort expectedDr, int? reportedJoinChannel = null, int? desiredJoinChannel = null)
        {
            var deviceJoinInfo = new DeviceJoinInfo(reportedJoinChannel, desiredJoinChannel);
            var datr = region.GetDownstreamRX2DataRate(nwksrvrx2dr, rx2drfromtwins, NullLogger.Instance, deviceJoinInfo);
            Assert.Equal(expectedDr, datr);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestTranslateToRegionData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestTranslateToRegionData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470RP1TestData.TestTranslateToRegionData), MemberType = typeof(RegionCN470RP1TestData))]
        [MemberData(nameof(RegionCN470RP2TestData.TestTranslateToRegionData), MemberType = typeof(RegionCN470RP2TestData))]
        [MemberData(nameof(RegionAS923TestData.TestTranslateToRegionData), MemberType = typeof(RegionAS923TestData))]
        public void TestTranslateToRegion(Region region, LoRaRegionType loRaRegion)
        {
            Assert.True(RegionManager.TryTranslateToRegion(loRaRegion, out var translatedRegion));
            Assert.IsType(region.GetType(), translatedRegion);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestTryGetJoinChannelIndexData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestTryGetJoinChannelIndexData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470RP1TestData.TestTryGetJoinChannelIndexData), MemberType = typeof(RegionCN470RP1TestData))]
        [MemberData(nameof(RegionCN470RP2TestData.TestTryGetJoinChannelIndexData), MemberType = typeof(RegionCN470RP2TestData))]
        [MemberData(nameof(RegionAS923TestData.TestTryGetJoinChannelIndexData), MemberType = typeof(RegionAS923TestData))]
        public void TestTryGetJoinChannelIndex(Region region, Hertz freq, int expectedIndex)
        {
            Assert.Equal(expectedIndex != -1, region.TryGetJoinChannelIndex(freq, out var channelIndex));
            Assert.Equal(expectedIndex, channelIndex);
        }

        [Theory]
        [MemberData(nameof(RegionEU868TestData.TestIsValidRX1DROffsetData), MemberType = typeof(RegionEU868TestData))]
        [MemberData(nameof(RegionUS915TestData.TestIsValidRX1DROffsetData), MemberType = typeof(RegionUS915TestData))]
        [MemberData(nameof(RegionCN470RP1TestData.TestIsValidRX1DROffsetData), MemberType = typeof(RegionCN470RP1TestData))]
        [MemberData(nameof(RegionCN470RP2TestData.TestIsValidRX1DROffsetData), MemberType = typeof(RegionCN470RP2TestData))]
        [MemberData(nameof(RegionAS923TestData.TestIsValidRX1DROffsetData), MemberType = typeof(RegionAS923TestData))]
        public void TestIsValidRX1DROffset(Region region, int offset, bool isValid)
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
        [MemberData(nameof(RegionCN470RP1TestData.TestIsDRIndexWithinAcceptableValuesData), MemberType = typeof(RegionCN470RP1TestData))]
        [MemberData(nameof(RegionCN470RP2TestData.TestIsDRIndexWithinAcceptableValuesData), MemberType = typeof(RegionCN470RP2TestData))]
        [MemberData(nameof(RegionAS923TestData.TestIsDRIndexWithinAcceptableValuesData), MemberType = typeof(RegionAS923TestData))]
        public void TestIsDRIndexWithinAcceptableValues(Region region, ushort? datarate, bool upstream, bool isValid)
        {
            if (upstream)
            {
                Assert.NotNull(datarate);
#pragma warning disable CWE476 // false positive
                Assert.Equal(isValid, region.RegionLimits.IsCurrentUpstreamDRIndexWithinAcceptableValue((ushort)datarate));
#pragma warning restore CWE476 // false positive
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
