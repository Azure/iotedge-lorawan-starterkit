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

    public abstract class MacCommandTests<T> where T : MacCommand
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
        protected T Subject { get; }

        /// <summary>
        /// Gets the list of data bytes (excluding the CID).
        /// </summary>
        protected IReadOnlyList<byte> DataBytes { get; }

        /// <summary>
        /// Gets the expected length of the MAC command.
        /// </summary>
        protected int Length => DataBytes.Count + 1;

        [Fact]
        public void ToBytes_Success() => ToBytes_Internal(Subject, DataBytes);

        protected void ToBytes_Internal(MacCommand macCommand, IReadOnlyList<byte> expectedBytes) =>
            Assert.Equal(GetFullBytes(expectedBytes), macCommand.ToBytes());

        [Fact]
        public void Length_Success() => Assert.Equal(Length, Subject.Length);

        [Fact]
        public void Cid_Success() => Assert.Equal(Cid, Subject.Cid);

        protected void DeserializationTest(Func<T, object> transform, string json) =>
            Assert.Equal(transform(Subject), transform(JsonConvert.DeserializeObject<T>(JsonUtil.Strictify(json)) ?? throw new InvalidOperationException("JSON was deserialized to null.")));

        protected void FromBytesTest(Func<T, object> transform, Func<byte[], T> subject) =>
            Assert.Equal(transform(Subject), transform(subject(GetFullBytes(DataBytes))));

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
        public void FromBytes_Success() => FromBytesTest(command => new { command.Cid, command.Battery, command.Margin },
                                                         bytes => new DevStatusAnswer(bytes.AsSpan()));
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
            DeserializationTest(command => new { command.Cid }, "{cid:6}");
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
            DeserializationTest(command => new { command.Cid, command.DutyCyclePL }, "{cid:4,dutyCyclePL:3}");
    }

    public sealed class LinkAdrAnswerTests : MacCommandTests<LinkADRAnswer>
    {
        public LinkAdrAnswerTests() :
            base(Cid.LinkADRCmd,
                 new LinkADRAnswer(1, true, false),
                 new byte[] { 0b110 })
        { }

        [Fact]
        public void FromBytes_Success() => FromBytesTest(command => new { command.Cid, command.PowerAck, command.DRAck, command.CHMaskAck },
                                                         bytes => new LinkADRAnswer(bytes.AsSpan()));
    }

    public sealed class LinkAdrRequestTests : MacCommandTests<LinkADRRequest>
    {
        private static object Transform(LinkADRRequest command) => new
        {
            command.Cid,
            command.DataRate,
            command.TxPower,
            command.ChMask,
            command.ChMaskCntl,
            command.NbRep
        };

        public LinkAdrRequestTests() :
            base(Cid.LinkADRCmd,
                 new LinkADRRequest(1, 2, 3, 4, 5),
                 new byte[] { 0b10010, 0b11, 0, 0b1000101 })
        { }

        [Fact]
        public void Deserializes_Correctly() =>
            DeserializationTest(Transform, "{cid:3,dataRate:1,txPower:2,chMask:3,chMaskCntl:4,nbRep:5}");

        [Fact]
        public void FromBytes_Success() => FromBytesTest(Transform, bytes => new LinkADRRequest(bytes));
    }

    public sealed class LinkCheckAnswerTests : MacCommandTests<LinkCheckAnswer>
    {
        public LinkCheckAnswerTests() :
            base(Cid.LinkCheckCmd,
                 new LinkCheckAnswer(1, 2),
                 new byte[] { 1, 2 })
        { }

        [Fact]
        public void FromBytes_Success() => FromBytesTest(command => new { command.Cid, command.Margin, command.GwCnt },
                                                         bytes => new LinkCheckAnswer(bytes.AsSpan()));
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
        public void FromBytes_Success() => FromBytesTest(command => new { command.Cid, command.DataRangeOk, command.ChannelFreqOk },
                                                         bytes => new NewChannelAnswer(bytes.AsSpan()));
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
            DeserializationTest(command => new { command.Cid, command.ChIndex, command.Freq, command.MaxDR, command.MinDR },
                                "{cid:7,chIndex:1,freq:2,drRange:52}");
    }

    public sealed class RxParamSetupAnswerTests : MacCommandTests<RXParamSetupAnswer>
    {
        public RxParamSetupAnswerTests() :
            base(Cid.RXParamCmd,
                 new RXParamSetupAnswer(true, false, true),
                 new byte[] { 0b101 })
        { }

        [Fact]
        public void FromBytes_Success() => FromBytesTest(command => new { command.Cid, command.Rx1DROffsetAck, command.Rx2DROffsetAck, command.ChannelAck },
                                                         bytes => new RXParamSetupAnswer(bytes.AsSpan()));
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
            DeserializationTest(command => new { command.Cid, command.Frequency, command.RX1DROffset, command.RX2DataRate },
                                "{cid:5,frequency:3,dlSettings:18}");
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
            DeserializationTest(command => new { command.Cid, command.Settings },
                                "{cid:8,settings:1}");
    }
}
