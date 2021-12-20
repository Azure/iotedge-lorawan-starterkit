// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using global::LoRaTools.Regions;
    using Xunit;

    public sealed class RegionAS923Tests
    {
        [Fact]
        public void DefaultDwellTimeSettings()
        {
            Assert.Equal(new DwellTimeSetting(true, true, 5), new RegionAS923().DefaultDwellTimeSetting);
        }
    }
}
