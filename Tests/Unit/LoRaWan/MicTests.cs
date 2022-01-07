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
            var appKey = AppKey.Parse("00000000000000000005100000000004");
            var mhdr = new MacHeader(0);
            var mic = Mic.ComputeForJoinRequest(appKey, mhdr, joinEui, devEui, devNonce);
            Assert.Equal(new Mic(0xb6dee36c), mic);
        }

        [Fact]
        public void ComputeForJoinRequest_NetworkSessionKey()
        {
            var joinEui = JoinEui.Parse("00-05-10-00-00-00-00-04");
            var devEui = DevEui.Parse("00-05-10-00-00-00-00-04");
            var devNonce = DevNonce.Read(new byte[] { 0xab, 0xcd });
            var appKey = NetworkSessionKey.Parse("00000000000000000005100000000004");
            var mhdr = new MacHeader(0);
            var mic = Mic.ComputeForJoinRequest(appKey, mhdr, joinEui, devEui, devNonce);
            Assert.Equal(new Mic(0xb6dee36c), mic);
        }
    }
}
