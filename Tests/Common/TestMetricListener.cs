// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using LoRaWan.NetworkServer;
    using Microsoft.Extensions.Logging.Abstractions;

    public sealed class TestMetricListener : RegistryMetricExporter
    {
        private readonly ConcurrentBag<(Instrument Instrument, double Value, KeyValuePair<string, object>[] Tags)> recordedMetrics =
            new ConcurrentBag<(Instrument, double, KeyValuePair<string, object>[])>();

        public IReadOnlyCollection<(Instrument Instrument, double Value, KeyValuePair<string, object>[] Tags)> RecordedMetrics => this.recordedMetrics;

        public TestMetricListener(string metricNamespace)
            : base(metricNamespace, MetricRegistry.RegistryLookup, NullLogger<RegistryMetricExporter>.Instance)
        { }

        protected override void TrackValue(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object>> tags, object state) =>
            this.recordedMetrics.Add((instrument, measurement, tags.ToArray()));
    }
}
