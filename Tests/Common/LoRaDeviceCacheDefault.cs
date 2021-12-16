// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using LoRaWan.NetworkServer;
    using Microsoft.Extensions.Logging.Abstractions;
    using System;

    public static class LoRaDeviceCacheDefault
    {
        public static LoRaDeviceCache CreateDefault() => new LoRaDeviceCache(new LoRaDeviceCacheOptions { MaxUnobservedLifetime = TimeSpan.FromMilliseconds(int.MaxValue), RefreshInterval = TimeSpan.FromMilliseconds(int.MaxValue), ValidationInterval = TimeSpan.FromMilliseconds(int.MaxValue) }, new NetworkServerConfiguration { GatewayID = MessageProcessorTestBase.ServerGatewayID }, NullLogger<LoRaDeviceCache>.Instance, TestMeter.Instance);
    }
}
