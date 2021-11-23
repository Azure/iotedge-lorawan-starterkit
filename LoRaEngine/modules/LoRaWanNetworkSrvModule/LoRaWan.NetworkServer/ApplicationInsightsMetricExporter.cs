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
    /// Exports metrics to Application Insights that are registered in MetricExporter and which have values of type int or double.
    /// </summary>
    internal class ApplicationInsightsMetricExporter : IMetricExporter
    {
        private readonly IDictionary<string, MetricIdentifier> metricRegistry;

        private readonly TelemetryClient telemetryClient;
        private MeterListener? listener;
        private bool disposedValue;

        public ApplicationInsightsMetricExporter(TelemetryClient telemetryClient) : this(telemetryClient, MetricExporter.RegistryLookup)
        { }

        internal ApplicationInsightsMetricExporter(TelemetryClient telemetryClient, IDictionary<string, CustomMetric> registryLookup)
        {
            this.telemetryClient = telemetryClient;
            this.metricRegistry = registryLookup.ToDictionary(m => m.Key, m => new MetricIdentifier(MetricExporter.Namespace, m.Value.Name, m.Value.Tags));
        }

        public void Start()
        {
            this.listener = new MeterListener
            {
                InstrumentPublished = (instrument, meterListener) =>
                {
                    if (instrument.Meter.Name == MetricExporter.Namespace && this.metricRegistry.ContainsKey(instrument.Name))
                        meterListener.EnableMeasurementEvents(instrument);
                }
            };

            this.listener.SetMeasurementEventCallback<int>((i, m, t, s) => TrackValue(i, m, t, s));
            this.listener.SetMeasurementEventCallback<double>(TrackValue);
            this.listener.Start();

            void TrackValue(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
            {
                if (this.metricRegistry.TryGetValue(instrument.Name, out var metricIdentifier))
                {
                    var m = this.telemetryClient.GetMetric(metricIdentifier);
                    var tagNames = metricIdentifier.GetDimensionNames().ToArray() ?? Array.Empty<string>();
                    this.TrackValue(m, measurement, MetricExporterHelper.GetTagsInOrder(tagNames, tags));
                }
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
