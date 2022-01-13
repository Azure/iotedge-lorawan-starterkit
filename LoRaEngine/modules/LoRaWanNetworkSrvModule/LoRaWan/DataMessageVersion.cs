// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    /// <summary>
    /// Represents the major version of the data message, which is "Major" bit field of the
    /// MAC Header (MHDR) field.
    /// </summary>
    /// <remarks>
    /// See section 4.2.2 (Major version of data message) of the LoRaWAN 1.0.3 Specification.
    /// </remarks>
    public enum DataMessageVersion
    {
        R1 = 0, // LoRaWAN R1
    }
}
