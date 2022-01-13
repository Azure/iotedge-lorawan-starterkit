// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1008 // Enums should have zero value (use nullables instead)

namespace LoRaWan
{
    /// <summary>
    /// The amount of spreading code applied to the original data signal. LoRa modulation has a
    /// total of six spreading factors (SF7 to SF12). The larger the spreading factor used, the
    /// farther the signal will be able to travel and still be received without errors by the RF
    /// receiver.
    /// </summary>
    public enum SpreadingFactor
    {
        SF7 = 7,
        SF8 = 8,
        SF9 = 9,
        SF10 = 10,
        SF11 = 11,
        SF12 = 12,
    }
}
