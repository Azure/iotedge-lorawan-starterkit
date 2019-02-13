// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    public enum PhysicalIdentifier
    {
        PUSH_DATA,
        PUSH_ACK,
        PULL_DATA,
        PULL_RESP,
        PULL_ACK,
        TX_ACK,
        UNKNOWN = byte.MaxValue
    }
}
