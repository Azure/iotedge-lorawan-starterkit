// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Net.NetworkInformation;
    using LoRaWan.NetworkServer;
    using NetworkServer;
    using Xunit;

    public class PhysicalAddressExtensionsTests
    {
        [Fact]
        public void Convert48To64_Succeeds_WithValidPhysicalAddress()
        {
            var physicalAddress48 = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 };
            var physicalAddress = new PhysicalAddress(physicalAddress48);

            var id6Mac = physicalAddress.Convert48To64();
            Assert.Equal(0x1122_33FF_FE44_5566UL, id6Mac);
        }

        [Fact]
        public void Convert48To64_Throw_When_Address_Is_Null()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => PhysicalAddressExtensions.Convert48To64(null));
            Assert.Equal("address", ex.ParamName);
        }

        [Fact]
        public void Convert48To64_Supports_48_Bits_Wide_Addresses_Only()
        {
            var input = new PhysicalAddress(new byte[8]);
            Assert.Throws<NotSupportedException>(() => input.Convert48To64());
        }
    }
}
