// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Buffers.Binary;
    using System.Net.NetworkInformation;

    internal static class PhysicalAddressExtensions
    {
        /// <summary>
        /// Converts a 48-bit physical address to a 64-bit physical address by settings the 4th
        /// octet to 0xFF and the 5th octet to 0xFE.
        /// </summary>
        /// <param name="address">
        /// The 48-bit physical address for which the ID6 MAC address representation should be
        /// computed.</param>
        public static ulong Convert48To64(this PhysicalAddress address)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));

            // As per specification (https://doc.sm.tc/station/glossary.html#term-mac)
            // for an ID6 based on a MAC Address we expect FFFE in the middle
            var physicalAddress48 = address.GetAddressBytes();
            if (physicalAddress48.Length != 6)
                throw new NotSupportedException("Physical addresses other than 48 bits wide are not supported.");

            Span<byte> physicalAddress64 = stackalloc byte[8];
            physicalAddress48[..3].CopyTo(physicalAddress64);
            physicalAddress64[3] = 0xFF;
            physicalAddress64[4] = 0xFE;
            physicalAddress48[3..].CopyTo(physicalAddress64[5..]);
            return BinaryPrimitives.ReadUInt64BigEndian(physicalAddress64);
        }
    }
}
