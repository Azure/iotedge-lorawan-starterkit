// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using System;
    using global::LoRaTools;
    using global::LoRaTools.Mac;
    using global::LoRaTools.Regions;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class TxParamSetupRequestTests
    {
        [Fact]
        public void Init_Throws_When_Dwell_Time_Settings_Are_Empty()
        {
            Assert.Throws<ArgumentNullException>(() => new TxParamSetupRequest(null));
        }

        [Fact]
        public void Init_Throws_When_Eirp_Is_Invalid()
        {
            Assert.Throws<ArgumentException>(() => new TxParamSetupRequest(new DwellTimeSetting(false, false, 16)));
        }

        public static TheoryData<DwellTimeSetting, byte> ToBytes_Theory_Data() => TheoryDataFactory.From(new[]
        {
            (new DwellTimeSetting(false, false, 0), (byte)0b0000_0000),
            (new DwellTimeSetting(true, false, 0), (byte)0b0010_0000),
            (new DwellTimeSetting(false, true, 0), (byte)0b0001_0000),
            (new DwellTimeSetting(false, false, 13), (byte)0b0000_1101),
            (new DwellTimeSetting(true, true, 15), (byte)0b0011_1111),
        });

        [Theory]
        [MemberData(nameof(ToBytes_Theory_Data))]
        public void ToByte_Success_Cases(DwellTimeSetting dwellTimeSetting, byte actualByte)
        {
            Assert.Equal(new[] { actualByte, (byte)Cid.TxParamSetupCmd }, new TxParamSetupRequest(dwellTimeSetting).ToBytes());
        }

        [Fact]
        public void Length()
        {
            Assert.Equal(2, new TxParamSetupRequest(new DwellTimeSetting(false, false, 0)).Length);
        }

        [Fact]
        public void Cid_Success()
        {
            Assert.Equal(Cid.TxParamSetupCmd, new TxParamSetupRequest(new DwellTimeSetting(false, false, 0)).Cid);
        }
    }
}
