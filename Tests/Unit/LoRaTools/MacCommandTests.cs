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

    public class MacCommandTests<T> where T : MacCommand
    {
        protected MacCommandTests(Cid cid, T subject, IReadOnlyList<byte> dataBytes)
        {
            Cid = cid;
            Subject = subject;
            DataBytes = dataBytes;
        }

        /// <summary>
        /// Gets the CID of the MAC command.
        /// </summary>
        protected Cid Cid { get; }

        /// <summary>
        /// Gets a default MAC command test subject.
        /// </summary>
        public T Subject { get; }

        /// <summary>
        /// Gets the list of data bytes (excluding the CID).
        /// </summary>
        public IReadOnlyList<byte> DataBytes { get; }

        /// <summary>
        /// Gets the expected length of the MAC command.
        /// </summary>
        public int Length => DataBytes.Count + 1;

        [Fact]
        public void ToBytes_Success() => ToBytes_Internal(Subject, DataBytes);

        protected void ToBytes_Internal(MacCommand macCommand, IReadOnlyList<byte> expectedBytes) =>
            Assert.Equal(GetFullBytes(expectedBytes), macCommand.ToBytes());

        [Fact]
        public void Length_Success() => Assert.Equal(Length, Subject.Length);

        [Fact]
        public void Cid_Success() => Assert.Equal(Cid, Subject.Cid);

        protected static void DeserializationTest(Predicate<T> assert, string json) =>
            Assert.True(assert(JsonConvert.DeserializeObject<T>(JsonUtil.Strictify(json)) ?? throw new InvalidOperationException("JSON was deserialized to null.")));

        protected void FromBytesTest(Predicate<T> assert, Func<byte[], T> subject, IReadOnlyList<byte> dataBytes) =>
            Assert.True(assert(subject(GetFullBytes(dataBytes))));

        private byte[] GetFullBytes(IReadOnlyList<byte> dataBytes) => dataBytes.Prepend((byte)Cid).ToArray();
    }

    public sealed class TxParamSetupRequestTests : MacCommandTests<TxParamSetupRequest>
    {
        public TxParamSetupRequestTests() :
            base(Cid.TxParamSetupCmd,
                 new TxParamSetupRequest(new DwellTimeSetting(false, false, 0)),
                 new byte[] { 0b0000_0000 })
        { }

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

    public sealed class TxParamSetupAnswerTests : MacCommandTests<TxParamSetupAnswer>
    {
        public TxParamSetupAnswerTests() :
            base(Cid.TxParamSetupCmd,
                 new TxParamSetupAnswer(),
                 Array.Empty<byte>())
        { }
    }

    public sealed class DevStatusAnswerTests : MacCommandTests<DevStatusAnswer>
    {
        public DevStatusAnswerTests() :
            base(Cid.DevStatusCmd,
                 new DevStatusAnswer(1, 2),
                 new byte[] { 1, 2 })
        { }

        [Fact]
        public void FromBytes_Success() => FromBytesTest(actual => actual.Cid == Cid
                                                                && actual.Battery == Subject.Battery
                                                                && actual.Margin == Subject.Margin,
                                                         bytes => new DevStatusAnswer(new ReadOnlySpan<byte>(bytes)),
                                                         DataBytes);
    }

    public sealed class DevStatusRequestTests : MacCommandTests<DevStatusRequest>
    {
        public DevStatusRequestTests() :
            base(Cid.DevStatusCmd,
                 new DevStatusRequest(),
                 Array.Empty<byte>())
        { }

        [Fact]
        public void Deserializes_Correctly() =>
            DeserializationTest(actual => actual.Cid == Cid, "{'cid':6}");
    }

    public sealed class DutyCycleAnswerTests : MacCommandTests<DutyCycleAnswer>
    {
        public DutyCycleAnswerTests() :
            base(Cid.DutyCycleCmd,
                 new DutyCycleAnswer(),
                 Array.Empty<byte>())
        { }
    }

    public sealed class DutyCycleRequestTests : MacCommandTests<DutyCycleRequest>
    {
        public DutyCycleRequestTests() :
            base(Cid.DutyCycleCmd,
                 new DutyCycleRequest(3),
                 new byte[] { 3 })
        { }

        [Fact]
        public void Deserializes_Correctly() =>
            DeserializationTest(actual => actual.Cid == Cid
                                       && actual.DutyCyclePL == Subject.DutyCyclePL,
                                "{'cid':4,'dutyCyclePL':3}");
    }

    public sealed class LinkAdrAnswerTests : MacCommandTests<LinkADRAnswer>
    {
        public LinkAdrAnswerTests() :
            base(Cid.LinkADRCmd,
                 new LinkADRAnswer(1, true, false),
                 new byte[] { 0b110 })
        { }

        [Fact]
        public void FromBytes_Success() => FromBytesTest(actual => actual.Cid == Cid
                                                                && actual.PowerAck
                                                                && actual.DRAck
                                                                && !actual.CHMaskAck,
                                                         bytes => new LinkADRAnswer(new ReadOnlySpan<byte>(bytes)),
                                                         DataBytes);
    }

    public sealed class LinkAdrRequestTests : MacCommandTests<LinkADRRequest>
    {
        public LinkAdrRequestTests() :
            base(Cid.LinkADRCmd,
                 new LinkADRRequest(1, 2, 3, 4, 5),
                 new byte[] { 0b10010, 0b11, 0, 0b1000101 })
        { }

        private Predicate<LinkADRRequest> Assert => actual => actual.Cid == Cid
                                                           && actual.DataRate == Subject.DataRate
                                                           && actual.TxPower == Subject.TxPower
                                                           && actual.ChMask == Subject.ChMask
                                                           && actual.ChMaskCntl == Subject.ChMaskCntl
                                                           && actual.NbRep == Subject.NbRep;

        [Fact]
        public void Deserializes_Correctly() =>
            DeserializationTest(Assert, "{'cid':3,'dataRate':1,'txPower':2,'chMask':3,'chMaskCntl':4,'nbRep':5}");

        [Fact]
        public void FromBytes_Success() => FromBytesTest(Assert, bytes => new LinkADRRequest(bytes), DataBytes);
    }

    public sealed class LinkCheckAnswerTests : MacCommandTests<LinkCheckAnswer>
    {
        public LinkCheckAnswerTests() :
            base(Cid.LinkCheckCmd,
                 new LinkCheckAnswer(1, 2),
                 new byte[] { 1, 2 })
        { }

        [Fact]
        public void FromBytes_Success() => FromBytesTest(actual => actual.Cid == Cid
                                                                && actual.Margin == Subject.Margin
                                                                && actual.GwCnt == Subject.GwCnt,
                                                         bytes => new LinkCheckAnswer(new ReadOnlySpan<byte>(bytes)),
                                                         DataBytes);
    }

    public sealed class LinkCheckRequestTests : MacCommandTests<LinkCheckRequest>
    {
        public LinkCheckRequestTests() :
            base(Cid.LinkCheckCmd,
                 new LinkCheckRequest(),
                 Array.Empty<byte>())
        { }
    }

    public sealed class NewChannelAnswerTests : MacCommandTests<NewChannelAnswer>
    {
        public NewChannelAnswerTests() :
            base(Cid.NewChannelCmd,
                 new NewChannelAnswer(false, true),
                 new byte[] { 1 })
        { }

        [Fact]
        public void FromBytes_Success() => FromBytesTest(actual => actual.Cid == Cid
                                                                && !actual.DataRangeOk
                                                                && actual.ChannelFreqOk,
                                                         bytes => new NewChannelAnswer(new ReadOnlySpan<byte>(bytes)),
                                                         DataBytes);
    }

    public sealed class NewChannelRequestTests : MacCommandTests<NewChannelRequest>
    {
        public NewChannelRequestTests() :
            base(Cid.NewChannelCmd,
                 new NewChannelRequest(1, 2, 3, 4),
                 new byte[] { 1, 2, 0, 0, 0b110100 })
        { }

        [Fact]
        public void Deserializes_Correctly() =>
            DeserializationTest(actual => actual.Cid == Cid
                                       && actual.ChIndex == Subject.ChIndex
                                       && actual.Freq == Subject.Freq
                                       && actual.MaxDR == Subject.MaxDR
                                       && actual.MinDR == Subject.MinDR,
                                "{'cid':7,'chIndex':1,'freq':2,'drRange':52}");
    }

    public sealed class RxParamSetupAnswerTests : MacCommandTests<RXParamSetupAnswer>
    {
        public RxParamSetupAnswerTests() :
            base(Cid.RXParamCmd,
                 new RXParamSetupAnswer(true, false, true),
                 new byte[] { 0b101 })
        { }

        [Fact]
        public void FromBytes_Success() => FromBytesTest(actual => actual.Cid == Cid
                                                                && actual.Rx1DROffsetAck
                                                                && !actual.Rx2DROffsetAck
                                                                && actual.ChannelAck,
                                                         bytes => new RXParamSetupAnswer(new ReadOnlySpan<byte>(bytes)),
                                                         DataBytes);
    }

    public sealed class RxParamSetupRequestTests : MacCommandTests<RXParamSetupRequest>
    {
        public RxParamSetupRequestTests() :
            base(Cid.RXParamCmd,
                 new RXParamSetupRequest(1, 2, 3),
                 new byte[] { 0b10010, 3, 0, 0 })
        { }

        [Fact]
        public void Deserializes_Correctly() =>
            DeserializationTest(actual => actual.Cid == Cid
                                       && actual.Frequency == Subject.Frequency
                                       && actual.RX1DROffset == Subject.RX1DROffset
                                       && actual.RX2DataRate == Subject.RX2DataRate,
                                "{'cid':5,'frequency':3,'dlSettings':18}");
    }

    public sealed class RxTimingSetupAnswerTests : MacCommandTests<RXTimingSetupAnswer>
    {
        public RxTimingSetupAnswerTests() :
            base(Cid.RXTimingCmd,
                 new RXTimingSetupAnswer(),
                 Array.Empty<byte>())
        { }
    }

    public sealed class RxTimingSetupRequestTests : MacCommandTests<RXTimingSetupRequest>
    {
        public RxTimingSetupRequestTests() :
            base(Cid.RXTimingCmd,
                 new RXTimingSetupRequest(1),
                 new byte[] { 1 })
        { }

        [Fact]
        public void Deserializes_Correctly() =>
            DeserializationTest(actual => actual.Cid == Cid && actual.Settings == Subject.Settings,
                                "{'cid':8,'settings':1}");
    }
}
