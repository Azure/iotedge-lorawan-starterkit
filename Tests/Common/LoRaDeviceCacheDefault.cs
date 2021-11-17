// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using LoRaWan.NetworkServer;
    using System;

    public static class LoRaDeviceCacheDefault
    {
        public static LoRaDeviceCache CreateDefault() => new LoRaDeviceCache(new LoRaDeviceCacheOptions { MaxUnobservedLifetime = TimeSpan.MaxValue, RefreshInterval = TimeSpan.MaxValue, ValidationInterval = TimeSpan.MaxValue }, new NetworkServerConfiguration { GatewayID = MessageProcessorTestBase.ServerGatewayID });
    }
}
