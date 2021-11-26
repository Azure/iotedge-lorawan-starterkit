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
        public bool IsMacLayerTestFPort => this.value == 224;

        public Span<byte> Write(Span<byte> buffer)
        {
            buffer[0] = this.value;
            return buffer[Size..];
        }
    }
}
