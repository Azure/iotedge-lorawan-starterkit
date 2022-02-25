// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::LoRaTools;
    using global::LoRaTools.Mac;
    using global::LoRaTools.Regions;
    using LoRaWan.Tests.Common;
    using Xunit;

    public abstract class MacCommandTests
    {
        public abstract Cid Cid { get; }
        public abstract MacCommand Subject { get; }
        public abstract IReadOnlyList<byte> Bytes { get; }
        public abstract int Length { get; }

        public sealed class TxParamSetupRequestTests : MacCommandTests
        {
            public override Cid Cid => Cid.TxParamSetupCmd;
            public override MacCommand Subject => new TxParamSetupRequest(new DwellTimeSetting(false, false, 0));
            public override IReadOnlyList<byte> Bytes => new byte[] { 0b0000_0000 };
            public override int Length => 2;

            [Fact]
            public void Init_Throws_When_Dwell_Time_Settings_Are_Empty() =>
                Assert.Throws<ArgumentNullException>(() => new TxParamSetupRequest(null!));

            [Fact]
            public void Init_Throws_When_Eirp_Is_Invalid() =>
                Assert.Throws<ArgumentException>(() => new TxParamSetupRequest(new DwellTimeSetting(false, false, 16)));

            public static TheoryData<DwellTimeSetting, byte> ToBytes_Theory_Data() => TheoryDataFactory.From(new (DwellTimeSetting, byte)[]
            {
                (new DwellTimeSetting(false, false, 0), 0b0000_0000),
                (new DwellTimeSetting(true, false, 0), 0b0010_0000),
                (new DwellTimeSetting(false, true, 0), 0b0001_0000),
                (new DwellTimeSetting(false, false, 13), 0b0000_1101),
                (new DwellTimeSetting(true, true, 15), 0b0011_1111),
            });

            [Theory]
            [MemberData(nameof(ToBytes_Theory_Data))]
            public void ToByte_Success_Cases(DwellTimeSetting dwellTimeSetting, byte expected) =>
                ToBytes_Internal(new TxParamSetupRequest(dwellTimeSetting), new[] { expected });
        }

        public sealed class TxParamSetupAnswerTests : MacCommandTests
        {
            public override Cid Cid => Cid.TxParamSetupCmd;
            public override MacCommand Subject => new TxParamSetupAnswer();
            public override IReadOnlyList<byte> Bytes => Array.Empty<byte>();
            public override int Length => 1;
        }

        public sealed class DevStatusAnswerTests : MacCommandTests
        {
            public override Cid Cid => Cid.DevStatusCmd;
            public override MacCommand Subject => new DevStatusAnswer(1, 2);
            public override IReadOnlyList<byte> Bytes => new byte[] { 1, 2 };
            public override int Length => 3;
        }

        [Fact]
        public void ToBytes_Success() => ToBytes_Internal(Subject, Bytes);

        protected void ToBytes_Internal(MacCommand macCommand, IReadOnlyList<byte> expectedBytes) =>
            Assert.Equal(new[] { (byte)Cid }.Concat(expectedBytes), macCommand.ToBytes());

        [Fact]
        public void Length_Success() => Assert.Equal(Length, Subject.Length);

        [Fact]
        public void Cid_Success() => Assert.Equal(Cid, Subject.Cid);
    }
}
