// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Diagnostics.Metrics;
    using LoRaWan.NetworkServer;

    public static class TestMeter
    {
        public static readonly Meter Instance = new Meter(MetricRegistry.Namespace, MetricRegistry.Version);
    }
}
