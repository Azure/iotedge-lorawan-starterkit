// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using System;
    using global::LoRaTools.Regions;
    using LoRaWan;
    using Xunit;

    public sealed class DwellTimeLimitedRegionTests
    {
        [Fact]
        public void When_Accessing_Uninitialized_DefaultDwellTimeSetting_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new DwellTimeLimitedTestRegion().DefaultDwellTimeSetting);
            Assert.Throws<InvalidOperationException>(() => new DwellTimeLimitedTestRegion().DesiredDwellTimeSetting);
        }

        [Fact]
        public void Setting_And_Getting_DefaultDwellTimeSetting_Success()
        {
            var dwellTimeSetting = new DwellTimeSetting(true, false, 4);
            var subject = new DwellTimeLimitedTestRegion
            {
                DefaultDwellTimeSetting = dwellTimeSetting
            };
            Assert.Equal(dwellTimeSetting, subject.DefaultDwellTimeSetting);
        }

        [Fact]
        public void Setting_And_Getting_DesiredDwellTimeSetting_Success()
        {
            var dwellTimeSetting = new DwellTimeSetting(true, false, 4);
            var subject = new DwellTimeLimitedTestRegion
            {
                DesiredDwellTimeSetting = dwellTimeSetting
            };
            Assert.Equal(dwellTimeSetting, subject.DesiredDwellTimeSetting);
        }

        private class DwellTimeLimitedTestRegion : DwellTimeLimitedRegion
        {
            public DwellTimeLimitedTestRegion() : base(LoRaRegionType.AS923)
            { }

            public override global::LoRaTools.Utils.RX2ReceiveWindow GetDefaultRX2ReceiveWindow(DeviceJoinInfo? deviceJoinInfo = null) =>
                throw new NotImplementedException();

            [Obsolete]
            public override bool TryGetDownstreamChannelFrequency(global::LoRaTools.LoRaPhysical.Rxpk upstreamChannel, out double frequency, DeviceJoinInfo? deviceJoinInfo = null) =>
                throw new NotImplementedException();

            public override bool TryGetDownstreamChannelFrequency(Hertz upstreamFrequency, out Hertz downstreamFrequency, DataRateIndex? upstreamDataRate = null, DeviceJoinInfo? deviceJoinInfo = null) =>
                throw new NotImplementedException();

            public override void UseDwellTimeSetting(DwellTimeSetting dwellTimeSetting) =>
                throw new NotImplementedException();
        }
    }
}
