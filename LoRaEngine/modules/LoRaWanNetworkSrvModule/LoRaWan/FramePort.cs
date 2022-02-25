// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1028 // Enum Storage should be Int32

namespace LoRaWan
{
    public enum FramePort : byte
    {
        MacCommand = 0,
        AppMin = 1,
        DropConnectionCommand = 2,
        AppMax = 223,
        MacLayerTest = 224,
#pragma warning disable CA1700 // Do not name enum values 'Reserved'
        ReservedMin = 225,
        ReservedMax = 255,
#pragma warning restore CA1700 // Do not name enum values 'Reserved'
    }

    public static class FramePortExtensions
    {
        public static bool IsAppSpecific(this FramePort port) => port is >= FramePort.AppMin and <= FramePort.AppMax;
        public static bool IsReserved(this FramePort port) => port >= FramePort.ReservedMin;
    }
}
