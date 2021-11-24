// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using Prometheus;

    /// <summary>
    /// Exposes metrics raised via System.Diagnostics.Metrics and which are registered in MetricRegistry on a Prometheus endpoint.
    /// </summary>
    internal class PrometheusMetricExporter : RegistryMetricExporter
    {
        private readonly IDictionary<string, Counter> counters;
        private readonly IDictionary<string, Histogram> histograms;

        public PrometheusMetricExporter()
            : this(MetricRegistry.RegistryLookup)
        { }

        internal PrometheusMetricExporter(IDictionary<string, CustomMetric> registryLookup)
            : base(registryLookup)
        {
            this.counters = GetMetricsFromRegistry(MetricType.Counter, m => Metrics.CreateCounter(m.Name, m.Description, m.Tags));
            this.histograms = GetMetricsFromRegistry(MetricType.Histogram, m => Metrics.CreateHistogram(m.Name, m.Description, m.Tags));
        }

        private IDictionary<string, T> GetMetricsFromRegistry<T>(MetricType metricType, Func<CustomMetric, T> factory) =>
            this.registryLookup.Values.Where(m => m.Type == metricType)
                                      .Select(m => KeyValuePair.Create(m.Name, factory(m)))
                                      .ToDictionary(m => m.Key, m => m.Value);

        protected override void TrackValue(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
#pragma warning disable format
            Action<string, string[], double> trackMetric = instrument switch
            {
                Counter<double> or Counter<int> or
                Counter<short> or Counter<byte> or
                Counter<long> or Counter<decimal> or
                Counter<float>                              => IncCounter,
                Histogram<double> or Histogram<int> or
                Histogram<short> or Histogram<byte> or
                Histogram<long> or Histogram<decimal> or
                Histogram<float>                            => RecordHistogram,
                _                                           => throw new NotImplementedException()
            };
#pragma warning restore format

            var inOrderTags = MetricExporterHelper.GetTagsInOrder(this.registryLookup[instrument.Name].Tags, tags);
            trackMetric(instrument.Name, inOrderTags, measurement);
        }

        internal virtual void IncCounter(string metricName, string[] tags, double measurement) =>
            this.counters[metricName].WithLabels(tags).Inc(measurement);

        internal virtual void RecordHistogram(string metricName, string[] tags, double measurement) =>
            this.histograms[metricName].WithLabels(tags).Observe(measurement);
    }
}
