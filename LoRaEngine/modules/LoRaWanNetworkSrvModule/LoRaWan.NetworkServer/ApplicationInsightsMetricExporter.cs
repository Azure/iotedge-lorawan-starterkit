// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.Metrics;

    /// <summary>
    /// Exports System.Diagnostics.Metrics metrics which are registered in MetricRegistry to Application Insights.
    /// </summary>
    internal class ApplicationInsightsMetricExporter : RegistryMetricExporter
    {
        private readonly IDictionary<string, (Metric, MetricIdentifier)> metricRegistry;

        private readonly TelemetryClient telemetryClient;

        public ApplicationInsightsMetricExporter(TelemetryClient telemetryClient) : this(telemetryClient, MetricRegistry.RegistryLookup)
        { }

        internal ApplicationInsightsMetricExporter(TelemetryClient telemetryClient, IDictionary<string, CustomMetric> registryLookup)
            : base(registryLookup)
        {
            this.telemetryClient = telemetryClient;
            this.metricRegistry = registryLookup.ToDictionary(m => m.Key, m =>
            {
                var id = new MetricIdentifier(MetricRegistry.Namespace, m.Value.Name, m.Value.Tags);
                return (this.telemetryClient.GetMetric(id), id);
            });
        }

        protected override void TrackValue(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            if (this.metricRegistry.TryGetValue(instrument.Name, out var value))
            {
                var (metric, identifier) = value;
                var tagNames = identifier.GetDimensionNames().ToArray() ?? Array.Empty<string>();
                TrackValue(metric, measurement, MetricExporterHelper.GetTagsInOrder(tagNames, tags));
            }
        }

        internal virtual void TrackValue(Metric metric, double measurement, params string[] dimensions)
        {
            switch (dimensions.Length)
            {
                case 0: metric.TrackValue(measurement); break;
                case 1: _ = metric.TrackValue(measurement, dimensions[0]); break;
                case 2: _ = metric.TrackValue(measurement, dimensions[0], dimensions[1]); break;
                case 3: _ = metric.TrackValue(measurement, dimensions[0], dimensions[1], dimensions[2]); break;
                default: throw new NotImplementedException("Metrics tracking in Application Insights for more than three dimensions it not supported");
            }
        }
    }
}
