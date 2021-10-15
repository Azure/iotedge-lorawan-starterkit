// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    /// <summary>
    /// Global end-device ID in IEEE EUI-64 (64-bit Extended Unique Identifier) address space
    /// that uniquely identifies the end-device.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For OTAA devices, the DevEUI MUST be stored in the end-device before the Join procedure
    /// is executed. ABP devices do not need the DevEUI to be stored in the device itself, but
    /// it is recommended to do so.</para>
    /// <para>
    /// EUI are 8 bytes multi-octet fields and are transmitted as little endian.</para>
    /// </remarks>
    public partial struct DevEui { }

    /// <summary>
    /// Global application ID in IEEE EUI-64 (64-bit Extended Unique Identifier) address space
    /// that uniquely identifies the Join Server that is able to assist in the processing of
    /// the Join procedure and the session keys derivation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For OTAA devices, the JoinEUI MUST be stored in the end-device before the Join procedure
    /// is executed. The JoinEUI is not required for ABP only end-devices.</para>
    /// <para>
    /// EUI are 8 bytes multi-octet fields and are transmitted as little endian.</para>
    /// </remarks>
    public partial struct JoinEui { }

    /// <summary>
    /// ID in IEEE EUI-64 (64-bit Extended Unique Identifier) address space that uniquely identifies
    /// a station.
    /// </summary>
    /// <remarks>
    /// EUI are 8 bytes multi-octet fields and are transmitted as little endian.
    /// </remarks>
    public partial struct StationEui { }
}
