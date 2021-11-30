// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using LoRaWan;
    using Xunit;

    public class DevAddrTests
    {
        private readonly DevAddr subject = new(0xeb6f7bde);

        [Fact]
        public void Size()
        {
            Assert.Equal(4, DevAddr.Size);
        }

        [Fact]
        public void NetworkId()
        {
            Assert.Equal(0x75, this.subject.NetworkId);
        }

        [Fact]
        public void NetworkAddress()
        {
            Assert.Equal(0x16f7bde, this.subject.NetworkAddress);
        }

        [Fact]
        public void Write_Writes_Byte_And_Returns_Updated_Span()
        {
            var bytes = new byte[4];
            var remainingBytes = this.subject.Write(bytes);
            Assert.Equal(0, remainingBytes.Length);
            Assert.Equal(new byte[] { 222, 123, 111, 235 }, bytes);
        }

        [Fact]
        public void ToString_Returns_Hexadecimal_String()
        {
            Assert.Equal("EB6F7BDE", this.subject.ToString());
        }
    }
}
