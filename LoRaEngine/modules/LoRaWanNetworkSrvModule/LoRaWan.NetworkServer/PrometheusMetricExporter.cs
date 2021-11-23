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
    /// Exposes metrics that have values of type int or double on a Prometheus endpoint.
    /// </summary>
    internal class PrometheusMetricExporter : IMetricExporter
    {
        private MeterListener? listener;
        private bool disposedValue;
        private readonly IDictionary<string, Counter> counters;
        private readonly IDictionary<string, Histogram> histograms;
        private readonly IDictionary<string, CustomMetric> registryLookup;

        public PrometheusMetricExporter()
            : this(MetricExporter.RegistryLookup)
        { }

        internal PrometheusMetricExporter(IDictionary<string, CustomMetric> registryLookup)
        {
            this.registryLookup = registryLookup;
            this.counters = GetMetricsFromRegistry(MetricType.Counter, m => Metrics.CreateCounter(m.Name, m.Description));
            this.histograms = GetMetricsFromRegistry(MetricType.Histogram, m => Metrics.CreateHistogram(m.Name, m.Description));
        }

        private IDictionary<string, T> GetMetricsFromRegistry<T>(MetricType metricType, Func<CustomMetric, T> factory) =>
            this.registryLookup.Values.Where(m => m.Type == metricType)
                                      .Select(m => KeyValuePair.Create(m.Name, factory(m)))
                                      .ToDictionary(m => m.Key, m => m.Value);

        public void Start()
        {
            try
            {
                this.listener = new MeterListener
                {
                    InstrumentPublished = (instrument, meterListener) =>
                    {
                        if (instrument.Meter.Name == MetricExporter.Namespace && this.registryLookup.ContainsKey(instrument.Name))
                            meterListener.EnableMeasurementEvents(instrument);
                    }
                };

                this.listener.SetMeasurementEventCallback<int>((i, m, t, s) => TrackValue(i, m, t, s));
                this.listener.SetMeasurementEventCallback<double>(TrackValue);
                this.listener.Start();
            }
            catch (Exception)
            {
                this.listener?.Dispose();
                throw;
            }

            void TrackValue(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
            {
#pragma warning disable format
                Action<string, string[], double> trackMetric = instrument switch
                {
                    Counter<double> or Counter<int>     => IncCounter,
                    Histogram<double> or Histogram<int> => RecordHistogram,
                    _                                   => throw new NotImplementedException()
                };
#pragma warning restore format

                var inOrderTags = MetricExporterHelper.GetTagsInOrder(this.registryLookup[instrument.Name].Tags, tags);
                trackMetric(instrument.Name, inOrderTags, measurement);
            }
        }

        internal virtual void IncCounter(string metricName, string[] tags, double measurement) =>
            this.counters[metricName].WithLabels(tags).Inc(measurement);

        internal virtual void RecordHistogram(string metricName, string[] tags, double measurement) =>
            this.histograms[metricName].WithLabels(tags).Observe(measurement);

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.listener?.Dispose();
                }

                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
