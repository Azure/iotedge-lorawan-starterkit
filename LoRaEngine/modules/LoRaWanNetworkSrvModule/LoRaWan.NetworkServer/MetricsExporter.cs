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

    internal static class MetricsExporter
    {
        public const string Namespace = "LoRaWan";
        public const string MetricsVersion = "1.0";

        public const string GatewayIdTagName = "GatewayId";
    }

    internal class ApplicationInsightsMetricExporter : IDisposable
    {
        private static readonly IDictionary<string, MetricIdentifier> MetricRegistry = new Dictionary<string, MetricIdentifier>
        {
            ["SomeCounter"] = new MetricIdentifier(MetricsExporter.Namespace, "SomeCounter", MetricsExporter.GatewayIdTagName)
        };

        // private static readonly MetricIdentifier SomeCounter = new MetricIdentifier(MetricsExporter.Namespace, "SomeCounter", MetricsExporter.GatewayIdTagName);

        private readonly TelemetryClient telemetryClient;
        private MeterListener? listener;
        private bool disposedValue;

        public ApplicationInsightsMetricExporter(TelemetryClient telemetryClient)
        {
            this.telemetryClient = telemetryClient;
        }

        public void Start()
        {
            try
            {
                this.listener = new MeterListener
                {
                    InstrumentPublished = (instrument, meterListener) =>
                    {
                        if (instrument.Meter.Name == MetricsExporter.Namespace && MetricRegistry.ContainsKey(instrument.Name))
                            meterListener.EnableMeasurementEvents(instrument);

                        // TODO: create static method to get metric identifiers
                    }
                };

                this.listener.SetMeasurementEventCallback<int>(TrackValue);
                this.listener.SetMeasurementEventCallback<double>(TrackValue);
                this.listener.Start();
            }
            catch (Exception)
            {
                this.listener?.Dispose();
                throw;
            }

            void TrackValue<T>(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
            {
                if (MetricRegistry.TryGetValue(instrument.Name, out var metricIdentifier))
                {
                    var m = this.telemetryClient.GetMetric(metricIdentifier);
                    var dimensionNames = metricIdentifier.GetDimensionNames().ToArray() ?? Array.Empty<string>();
                    var t = new Dictionary<string, object?>(tags.ToArray(), StringComparer.OrdinalIgnoreCase);

                    var dimensions =
                        dimensionNames.Select(dn => t.TryGetValue(dn, out var v) ? (v?.ToString() ?? string.Empty) : string.Empty).ToArray();

                    this.TrackValue(m, measurement, dimensions);
                }
            }
        }

        internal virtual void TrackValue<T>(Metric metric, T measurement, params string[] dimensions)
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
