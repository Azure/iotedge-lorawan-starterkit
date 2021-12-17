// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1028 // Enum Storage should be Int32

namespace LoRaWan
{
    using System;

    public enum FramePort : byte
    {
        MacCommand = 0,
        MacLayerTest = 224
    }

    public static class FramePortExtensions
    {
        public static bool IsApplicationSpecific(this FramePort port) => port is > FramePort.MacCommand and < FramePort.MacLayerTest;
        public static bool IsReservedForFutureAplications(this FramePort port) => (byte)port >= 225;

        public static Span<byte> Write(this FramePort port, Span<byte> buffer)
        {
            buffer[0] = (byte)port;
            return buffer[1..];
        }
    }
}
