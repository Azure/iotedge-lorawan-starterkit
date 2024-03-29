// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using System;
    using System.Collections.Generic;
    using global::LoRaTools.Regions;
    using global::LoRaTools.Utils;
    using LoRaWan;
    using Xunit;

    public sealed class DwellTimeLimitedRegionTests
    {
        [Fact]
        public void When_Accessing_Uninitialized_DwellTimeSetting_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new DwellTimeLimitedTestRegion().DesiredDwellTimeSetting);
        }

        [Fact]
        public void Setting_And_Getting_DesiredDwellTimeSetting_Success()
        {
            var dwellTimeSetting = new DwellTimeSetting(true, false, 4);
            var subject = new DwellTimeLimitedTestRegion { DesiredDwellTimeSetting = dwellTimeSetting };
            Assert.Equal(dwellTimeSetting, subject.DesiredDwellTimeSetting);
        }

        private class DwellTimeLimitedTestRegion : DwellTimeLimitedRegion
        {
            public DwellTimeLimitedTestRegion() : base(LoRaRegionType.AS923)
            { }

            public override IReadOnlyDictionary<DataRateIndex, (DataRate DataRate, uint MaxPayloadSize)> DRtoConfiguration =>
                throw new NotImplementedException();

            public override IReadOnlyDictionary<uint, double> TXPowertoMaxEIRP => throw new NotImplementedException();

            public override IReadOnlyList<IReadOnlyList<DataRateIndex>> RX1DROffsetTable => throw new NotImplementedException();

            protected override DwellTimeSetting DefaultDwellTimeSetting => new DwellTimeSetting(true, false, 4);

            public override ReceiveWindow GetDefaultRX2ReceiveWindow(DeviceJoinInfo? deviceJoinInfo = null) =>
                throw new NotImplementedException();

            public override bool TryGetDownstreamChannelFrequency(Hertz upstreamFrequency, DataRateIndex upstreamDataRate, DeviceJoinInfo deviceJoinInfo, out Hertz downstreamFrequency) =>
                throw new NotImplementedException();

            public override void UseDwellTimeSetting(DwellTimeSetting dwellTimeSetting) =>
                throw new NotImplementedException();
        }
    }
}
