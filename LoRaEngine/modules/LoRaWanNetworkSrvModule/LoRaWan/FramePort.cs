// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;

    public readonly record struct FramePort
    {
        public const int Size = sizeof(byte);
        private readonly byte value;

        public FramePort(byte value) => this.value = value;

        public bool IsMacCommandFPort => this.value == 0;
        public bool IsApplicationSpecificFPort => this.value is > 0 and < 224;
        public bool IsMacLayerTestFPort => this.value == 224;
        public bool IsReservedForFutureAplicationsFPort => this.value is >= 225 and <= 255;

        public static explicit operator byte(FramePort fport) => fport.value;

        public Span<byte> Write(Span<byte> buffer)
        {
            buffer[0] = this.value;
            return buffer[Size..];
        }
    }
}
