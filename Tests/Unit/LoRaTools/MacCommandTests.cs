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
    using Newtonsoft.Json;
    using Xunit;

    public abstract class MacCommandTests
    {
        /// <summary>
        /// Gets the CID of the MAC command.
        /// </summary>
        public abstract Cid Cid { get; }

        /// <summary>
        /// Gets a default MAC command test subject.
        /// </summary>
        public abstract MacCommand Subject { get; }

        /// <summary>
        /// Gets the list of data bytes (excluding the CID).
        /// </summary>
        public abstract IReadOnlyList<byte> DataBytes { get; }

        /// <summary>
        /// Gets the expected length of the MAC command.
        /// </summary>
        public abstract int Length { get; }

        public sealed class TxParamSetupRequestTests : MacCommandTests
        {
            public override Cid Cid => Cid.TxParamSetupCmd;
            public override MacCommand Subject => new TxParamSetupRequest(new DwellTimeSetting(false, false, 0));
            public override IReadOnlyList<byte> DataBytes => new byte[] { 0b0000_0000 };
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
            public override IReadOnlyList<byte> DataBytes => Array.Empty<byte>();
            public override int Length => 1;
        }

        public sealed class DevStatusAnswerTests : MacCommandTests
        {
            public override Cid Cid => Cid.DevStatusCmd;
            public override DevStatusAnswer Subject => new DevStatusAnswer(1, 2);
            public override IReadOnlyList<byte> DataBytes => new byte[] { 1, 2 };
            public override int Length => 3;

            [Fact]
            public void FromBytes_Success() => FromBytesTest(actual => actual.Cid == Cid
                                                                    && actual.Battery == Subject.Battery
                                                                    && actual.Margin == Subject.Margin,
                                                             bytes => new DevStatusAnswer(new ReadOnlySpan<byte>(bytes)),
                                                             DataBytes);
        }

        public sealed class DevStatusRequestTests : MacCommandTests
        {
            public override Cid Cid => Cid.DevStatusCmd;
            public override MacCommand Subject => new DevStatusRequest();
            public override IReadOnlyList<byte> DataBytes => Array.Empty<byte>();
            public override int Length => 1;

            [Fact]
            public void Deserializes_Correctly() =>
                DeserializationTest<DevStatusRequest>(actual => actual.Cid == Cid, @"{""cid"":6}");
        }

        public sealed class DutyCycleAnswerTests : MacCommandTests
        {
            public override Cid Cid => Cid.DutyCycleCmd;
            public override MacCommand Subject => new DutyCycleAnswer();
            public override IReadOnlyList<byte> DataBytes => Array.Empty<byte>();
            public override int Length => 1;
        }

        public sealed class DutyCycleRequestTests : MacCommandTests
        {
            public override Cid Cid => Cid.DutyCycleCmd;
            public override DutyCycleRequest Subject => new DutyCycleRequest(3);
            public override IReadOnlyList<byte> DataBytes => new byte[] { 3 };
            public override int Length => 2;

            [Fact]
            public void Deserializes_Correctly() =>
                DeserializationTest<DutyCycleRequest>(actual => actual.Cid == Cid
                                                             && actual.DutyCyclePL == Subject.DutyCyclePL,
                                                      @"{""cid"":4,""dutyCyclePL"":3}");
        }

        public sealed class LinkAdrAnswerTests : MacCommandTests
        {
            public override Cid Cid => Cid.LinkADRCmd;
            public override LinkADRAnswer Subject => new LinkADRAnswer(true, true, false);
            public override IReadOnlyList<byte> DataBytes => new byte[] { 0b110 };
            public override int Length => 2;

            [Fact]
            public void FromBytes_Success() => FromBytesTest(actual => actual.Cid == Cid
                                                                    && actual.PowerAck
                                                                    && actual.DRAck
                                                                    && !actual.CHMaskAck,
                                                             bytes => new LinkADRAnswer(new ReadOnlySpan<byte>(bytes)),
                                                             DataBytes);
        }

        public sealed class LinkAdrRequestTests : MacCommandTests
        {
            public override Cid Cid => Cid.LinkADRCmd;
            public override LinkADRRequest Subject => new LinkADRRequest(1, 2, 3, 4, 5);
            public override IReadOnlyList<byte> DataBytes => new byte[] { 0b10010, 0b11, 0, 0b1000101 };
            public override int Length => 5;

            private Predicate<LinkADRRequest> Assert => actual => actual.Cid == Cid
                                                               && actual.DataRate == Subject.DataRate
                                                               && actual.TxPower == Subject.TxPower
                                                               && actual.ChMask == Subject.ChMask
                                                               && actual.ChMaskCntl == Subject.ChMaskCntl
                                                               && actual.NbRep == Subject.NbRep;

            [Fact]
            public void Deserializes_Correctly() =>
                DeserializationTest(Assert, @"{""cid"":3,""dataRate"":1,""txPower"":2,""chMask"":3,""chMaskCntl"":4,""nbRep"":5}");

            [Fact]
            public void FromBytes_Success() => FromBytesTest(Assert, bytes => new LinkADRRequest(bytes), DataBytes);
        }

        public sealed class LinkCheckAnswerTests : MacCommandTests
        {
            public override Cid Cid => Cid.LinkCheckCmd;
            public override LinkCheckAnswer Subject => new LinkCheckAnswer(1, 2);
            public override IReadOnlyList<byte> DataBytes => new byte[] { 1, 2 };
            public override int Length => 3;

            [Fact]
            public void FromBytes_Success() => FromBytesTest(actual => actual.Cid == Cid
                                                                    && actual.Margin == Subject.Margin
                                                                    && actual.GwCnt == Subject.GwCnt,
                                                             bytes => new LinkCheckAnswer(new ReadOnlySpan<byte>(bytes)),
                                                             DataBytes);
        }

        public sealed class LinkCheckRequestTests : MacCommandTests
        {
            public override Cid Cid => Cid.LinkCheckCmd;
            public override MacCommand Subject => new LinkCheckRequest();
            public override IReadOnlyList<byte> DataBytes => Array.Empty<byte>();
            public override int Length => 1;
        }

        public sealed class NewChannelAnswerTests : MacCommandTests
        {
            public override Cid Cid => Cid.NewChannelCmd;
            public override NewChannelAnswer Subject => new NewChannelAnswer(false, true);
            public override IReadOnlyList<byte> DataBytes => new byte[] { 1 };
            public override int Length => 2;

            [Fact]
            public void FromBytes_Success() => FromBytesTest(actual => actual.Cid == Cid
                                                                    && !actual.DataRangeOk
                                                                    && actual.ChannelFreqOk,
                                                             bytes => new NewChannelAnswer(new ReadOnlySpan<byte>(bytes)),
                                                             DataBytes);
        }

        public sealed class NewChannelRequestTests : MacCommandTests
        {
            public override Cid Cid => Cid.NewChannelCmd;
            public override NewChannelRequest Subject => new NewChannelRequest(1, 2, 3, 4);
            public override IReadOnlyList<byte> DataBytes => new byte[] { 1, 2, 0, 0, 0b110100 };
            public override int Length => 6;

            [Fact]
            public void Deserializes_Correctly() =>
                DeserializationTest<NewChannelRequest>(actual => actual.Cid == Cid
                                                              && actual.ChIndex == Subject.ChIndex
                                                              && actual.Freq == Subject.Freq
                                                              && actual.MaxDR == Subject.MaxDR
                                                              && actual.MinDR == Subject.MinDR,
                                                       @"{""cid"":7,""chIndex"":1,""freq"":2,""drRange"":52}");
        }

        public sealed class RxParamSetupAnswerTests : MacCommandTests
        {
            public override Cid Cid => Cid.RXParamCmd;
            public override RXParamSetupAnswer Subject => new RXParamSetupAnswer(true, false, true);
            public override IReadOnlyList<byte> DataBytes => new byte[] { 0b101 };
            public override int Length => 2;

            [Fact]
            public void FromBytes_Success() => FromBytesTest(actual => actual.Cid == Cid
                                                                    && actual.Rx1DROffsetAck
                                                                    && !actual.Rx2DROffsetAck
                                                                    && actual.ChannelAck,
                                                             bytes => new RXParamSetupAnswer(new ReadOnlySpan<byte>(bytes)),
                                                             DataBytes);
        }

        public sealed class RxParamSetupRequestTests : MacCommandTests
        {
            public override Cid Cid => Cid.RXParamCmd;
            public override RXParamSetupRequest Subject => new RXParamSetupRequest(1, 2, 3);
            public override IReadOnlyList<byte> DataBytes => new byte[] { 0b10010, 3, 0, 0 };
            public override int Length => 5;

            [Fact]
            public void Deserializes_Correctly() =>
                DeserializationTest<RXParamSetupRequest>(actual => actual.Cid == Cid
                                                                && actual.Frequency == Subject.Frequency
                                                                && actual.RX1DROffset == Subject.RX1DROffset
                                                                && actual.RX2DataRate == Subject.RX2DataRate,
                                                         @"{""cid"":5,""frequency"":3,""dlSettings"":18}");
        }

        public sealed class RxTimingSetupAnswerTests : MacCommandTests
        {
            public override Cid Cid => Cid.RXTimingCmd;
            public override MacCommand Subject => new RXTimingSetupAnswer();
            public override IReadOnlyList<byte> DataBytes => Array.Empty<byte>();
            public override int Length => 1;
        }

        public sealed class RxTimingSetupRequestTests : MacCommandTests
        {
            public override Cid Cid => Cid.RXTimingCmd;
            public override RXTimingSetupRequest Subject => new RXTimingSetupRequest(1);
            public override IReadOnlyList<byte> DataBytes => new byte[] { 1 };
            public override int Length => 2;

            [Fact]
            public void Deserializes_Correctly() =>
                DeserializationTest<RXTimingSetupRequest>(actual => actual.Cid == Cid && actual.Settings == Subject.Settings,
                                                          @"{""cid"":8,""settings"":1}");
        }

        [Fact]
        public void ToBytes_Success() => ToBytes_Internal(Subject, DataBytes);

        protected void ToBytes_Internal(MacCommand macCommand, IReadOnlyList<byte> expectedBytes) =>
            Assert.Equal(GetFullBytes(expectedBytes), macCommand.ToBytes());

        [Fact]
        public void Length_Success() => Assert.Equal(Length, Subject.Length);

        [Fact]
        public void Cid_Success() => Assert.Equal(Cid, Subject.Cid);

        protected static void DeserializationTest<T>(Predicate<T> assert, string json) =>
            Assert.True(assert(JsonConvert.DeserializeObject<T>(json) ?? throw new InvalidOperationException("JSON was deserialized to null.")));

        protected void FromBytesTest<T>(Predicate<T> assert, Func<byte[], T> subject, IReadOnlyList<byte> dataBytes) =>
            Assert.True(assert(subject(GetFullBytes(dataBytes))));

        private byte[] GetFullBytes(IReadOnlyList<byte> dataBytes) => dataBytes.Prepend((byte)Cid).ToArray();
    }
}
