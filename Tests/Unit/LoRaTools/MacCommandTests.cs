// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using System;
    using System.Linq;
    using global::LoRaTools;
    using global::LoRaTools.Mac;
    using global::LoRaTools.Regions;
    using LoRaWan.Tests.Common;
    using Xunit;

    public abstract class MacCommandTests
    {
        public abstract Cid Cid { get; }
        public abstract MacCommand ValidSubject { get; }
        public abstract int Length { get; }

        public sealed class TxParamSetupRequestTests : MacCommandTests
        {
            public override Cid Cid => Cid.TxParamSetupCmd;
            public override MacCommand ValidSubject => new TxParamSetupRequest(new DwellTimeSetting(false, false, 0));
            public override int Length => 2;

            [Fact]
            public void Init_Throws_When_Dwell_Time_Settings_Are_Empty() =>
                Assert.Throws<ArgumentNullException>(() => new TxParamSetupRequest(null!));

            [Fact]
            public void Init_Throws_When_Eirp_Is_Invalid() =>
                Assert.Throws<ArgumentException>(() => new TxParamSetupRequest(new DwellTimeSetting(false, false, 16)));

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
            public void ToByte_Success_Cases(DwellTimeSetting dwellTimeSetting, byte expected) =>
                ToBytes_Success(new TxParamSetupRequest(dwellTimeSetting), new[] { expected });
        }

        public sealed class TxParamSetupAnswerTests : MacCommandTests
        {
            public override Cid Cid => Cid.TxParamSetupCmd;
            public override MacCommand ValidSubject => new TxParamSetupAnswer();
            public override int Length => 1;

            [Fact]
            public void ToBytes() => ToBytes_Success(ValidSubject, Array.Empty<byte>());
        }

        protected void ToBytes_Success(MacCommand macCommand, byte[] expectedBytes) =>
            Assert.Equal(new[] { (byte)Cid }.Concat(expectedBytes), macCommand.ToBytes());

        [Fact]
        public void Length_Success() => Assert.Equal(Length, ValidSubject.Length);

        [Fact]
        public void Cid_Success() => Assert.Equal(Cid, ValidSubject.Cid);
    }
}
