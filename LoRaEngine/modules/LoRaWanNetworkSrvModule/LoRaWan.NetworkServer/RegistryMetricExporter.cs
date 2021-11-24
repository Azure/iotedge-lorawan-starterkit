// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;

    internal abstract class RegistryMetricExporter : IMetricExporter
    {
        protected readonly IDictionary<string, CustomMetric> registryLookup;

        private MeterListener? listener;
        private bool disposedValue;

        public RegistryMetricExporter(IDictionary<string, CustomMetric> registryLookup)
        {
            this.registryLookup = registryLookup;
        }

        public void Start()
        {
            this.listener = new MeterListener
            {
                InstrumentPublished = (instrument, meterListener) =>
                {
                    if (instrument.Meter.Name == MetricRegistry.Namespace && this.registryLookup.ContainsKey(instrument.Name))
                        meterListener.EnableMeasurementEvents(instrument);
                }
            };

            this.listener.SetMeasurementEventCallback<byte>((i, m, t, s) => TrackValue(i, m, t, s));
            this.listener.SetMeasurementEventCallback<short>((i, m, t, s) => TrackValue(i, m, t, s));
            this.listener.SetMeasurementEventCallback<int>((i, m, t, s) => TrackValue(i, m, t, s));
            this.listener.SetMeasurementEventCallback<long>((i, m, t, s) => TrackValue(i, m, t, s));
            this.listener.SetMeasurementEventCallback<float>((i, m, t, s) => TrackValue(i, m, t, s));
            this.listener.SetMeasurementEventCallback<double>(TrackValue);
            this.listener.SetMeasurementEventCallback<decimal>((i, m, t, s) => TrackValue(i, checked((double)m), t, s));
            this.listener.Start();
        }

        protected abstract void TrackValue(Instrument instrument,
                                           double measurement,
                                           ReadOnlySpan<KeyValuePair<string, object?>> tags,
                                           object? state);

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
