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
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Exports System.Diagnostics.Metrics metrics which are registered in MetricRegistry to Application Insights.
    /// Only non-observable metrics are currently supported.
    /// </summary>
    internal class ApplicationInsightsMetricExporter : RegistryMetricExporter
    {
        private readonly IDictionary<string, (Metric, string[])> metricRegistry;
        private readonly RegistryMetricTagBag metricTagBag;

        public ApplicationInsightsMetricExporter(TelemetryClient telemetryClient,
                                                 RegistryMetricTagBag metricTagBag,
                                                 ILogger<ApplicationInsightsMetricExporter> logger)
            : this(telemetryClient, MetricRegistry.RegistryLookup, metricTagBag, logger)
        { }

        internal ApplicationInsightsMetricExporter(TelemetryClient telemetryClient,
                                                   IDictionary<string, CustomMetric> registryLookup,
                                                   RegistryMetricTagBag metricTagBag,
                                                   ILogger<ApplicationInsightsMetricExporter> logger)
            : base(registryLookup, logger)
        {
            this.metricRegistry = registryLookup.ToDictionary(m => m.Key, m =>
            {
                var id = new MetricIdentifier(MetricRegistry.Namespace, m.Value.Name, m.Value.Tags);
                return (telemetryClient.GetMetric(id), id.GetDimensionNames().ToArray());
            });
            this.metricTagBag = metricTagBag;
        }

        protected override void TrackValue(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            if (this.metricRegistry.TryGetValue(instrument.Name, out var value))
            {
                var (metric, dimensions) = value;
                TrackValue(metric, measurement, MetricExporterHelper.GetTagsInOrder(dimensions, tags, this.metricTagBag));
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
                case 4: _ = metric.TrackValue(measurement, dimensions[0], dimensions[1], dimensions[2], dimensions[3]); break;
                default: throw new NotImplementedException("We do not support tracking more than four custom dimensions.");
            }
        }
    }
}
