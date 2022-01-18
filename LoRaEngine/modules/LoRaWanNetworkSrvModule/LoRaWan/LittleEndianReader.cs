// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;

    internal static class LittleEndianReader
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt24(ReadOnlySpan<byte> buffer) =>
            buffer.Length >= 3 ? buffer[0] | ((uint)buffer[1] << 8) | ((uint)buffer[2] << 16) : ThrowInsufficientBufferLength();

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static uint ThrowInsufficientBufferLength() =>
            throw new ArgumentException("Insufficient buffer length.", "buffer");
    }
}
