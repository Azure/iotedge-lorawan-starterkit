// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using LoRaWan;
    using LoRaWan.Tests.Common;
    using Xunit;

    public class MicTests
    {
        private readonly Mic subject = new(0x12345678);

        [Fact]
        public void Size()
        {
            Assert.Equal(4, Mic.Size);
        }

        [Fact]
        public void ToString_Returns_Hexadecimal_String()
        {
            Assert.Equal("12345678", this.subject.ToString());
        }

        [Fact]
        public void Write_Writes_Byte_And_Returns_Updated_Span()
        {
            var bytes = new byte[4];
            var remainingBytes = this.subject.Write(bytes);
            Assert.Equal(0, remainingBytes.Length);
            Assert.Equal(new byte[] { 120, 86, 52, 18 }, bytes);
        }

        public static TheoryData<Mic, byte[]> Read_Success_TheoryData() => TheoryDataFactory.From(new[]
        {
            (new Mic(1), new byte[] { 1, 0, 0, 0 }),
            (new Mic(1), new byte[] { 1, 0, 0, 0, 0 }),
            (new Mic(0x04030201), new byte[] { 1, 2, 3, 4 }),
        });

        [Theory]
        [MemberData(nameof(Read_Success_TheoryData))]
        public void Read_Success(Mic expected, byte[] buffer)
        {
            Assert.Equal(expected, Mic.Read(buffer));
        }

        [Fact]
        public void ComputeForJoinRequest_AppKey()
        {
            var joinEui = JoinEui.Parse("00-05-10-00-00-00-00-04");
            var devEui = DevEui.Parse("00-05-10-00-00-00-00-04");
            var devNonce = DevNonce.Read(new byte[] { 0xab, 0xcd });
            var key = TestKeys.CreateAppKey(0x0005100000000004);
            var mhdr = new MacHeader(0);
            var mic = Mic.ComputeForJoinRequest(key, mhdr, joinEui, devEui, devNonce);
            Assert.Equal(new Mic(unchecked((int)(0xb6dee36c))), mic);
        }

        [Fact]
        public void ComputeForJoinRequest_NetworkSessionKey()
        {
            var joinEui = JoinEui.Parse("00-05-10-00-00-00-00-04");
            var devEui = DevEui.Parse("00-05-10-00-00-00-00-04");
            var devNonce = DevNonce.Read(new byte[] { 0xab, 0xcd });
            var key = TestKeys.CreateNetworkSessionKey(0x0005100000000004);
            var mhdr = new MacHeader(0);
            var mic = Mic.ComputeForJoinRequest(key, mhdr, joinEui, devEui, devNonce);
            Assert.Equal(new Mic(unchecked((int)(0xb6dee36c))), mic);
        }

        [Fact]
        public void ComputeForJoinAccept()
        {
            var key = TestKeys.CreateAppKey(0x0005100000000004);
            var mhdr = new MacHeader(1);
            var joinNonce = new AppNonce(0xabcdef);
            var netId = new NetId(0xbcbbba);
            var devAddr = new DevAddr(0x14131211);
            var dlSettings = new byte[] { 0xca };
            var rxDelay = RxDelay.RxDelay10;
            var cfList = new byte[16] { 0xe1, 0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xea, 0xeb, 0xec, 0xed, 0xef, 0xf1, 0xf2, 0xf3 };
            var mic = Mic.ComputeForJoinAccept(key, mhdr, joinNonce, netId, devAddr, dlSettings, rxDelay, cfList);
            Assert.Equal(new Mic(-1539170837), mic);
        }

        [Fact]
        public void ComputeForData()
        {
            var key = TestKeys.CreateNetworkSessionKey(0x0005100000000004);
            byte direction = 3;
            var devAddr = new DevAddr(0xc1c2c3c4);
            var fcnt = 50462976U;
            var msg = new byte[] { 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16 };
            var mic = Mic.ComputeForData(key, direction, devAddr, fcnt, msg);
            Assert.Equal(new Mic(0x456CA231), mic);
        }
    }
}
