// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;

    /// <summary>
    /// Flags that disable certain regulatory transmission constraints in debug builds of a Station,
    /// such as clear channel assessment (<c>nocca</c>), duty-cycle (<c>nodc</c>) and dwell-time
    /// limitations (<c>nodwell</c>).
    /// </summary>
    [Flags]
    internal enum RouterConfigStationFlags
    {
        None,
        NoClearChannelAssessment = 1, // nocca
        NoDutyCycle = 2,              // nodc
        NoDwellTimeLimitations = 4,   // nodwell
    }
}
