﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;

    public interface ILoRaDeviceMessageDeduplicationStrategy
    {
        Task<DeduplicationResult> ResolveDeduplication(int fct, int? fcntDown, string gatewayId);
    }
}