// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using LoRaWan;
    using Xunit;

    public class NetIdTests
    {
        private readonly NetId subject = new(0x1a2b3c);

        [Fact]
        public void Size()
        {
            Assert.Equal(3, NetId.Size);
        }

        [Theory]
        [InlineData(0x1a2b3c, 0x3c)]
        [InlineData(0xffffff, 0x7f)]
        public void NetworkId_Returns_7_Lsb(int netId, int expectedNetworkId)
        {
            var subject = new NetId(netId);
            Assert.Equal(expectedNetworkId, subject.NetworkId);
        }

        [Fact]
        public void ToString_Returns_Hexadecimal_String()
        {
            Assert.Equal("1A2B3C", this.subject.ToString());
        }
    }
}
