// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    internal abstract class RegistryMetricExporter : IMetricExporter
    {
        private static readonly TimeSpan ObserveInterval = TimeSpan.FromSeconds(10);

        private readonly CancellationTokenSource cancellationTokenSource;
        protected readonly IDictionary<string, CustomMetric> registryLookup;
        private readonly ILogger<RegistryMetricExporter> logger;
        private MeterListener? listener;
        private bool disposedValue;

        public RegistryMetricExporter(IDictionary<string, CustomMetric> registryLookup, ILogger<RegistryMetricExporter> logger)
        {
            this.registryLookup = registryLookup;
            this.logger = logger;
            this.cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            this.listener = new MeterListener
            {
                InstrumentPublished = (instrument, meterListener) =>
                {
                    if (instrument.Meter.Name == MetricRegistry.Namespace && this.registryLookup.ContainsKey(instrument.Name))
                    {
                        meterListener.EnableMeasurementEvents(instrument);
                    }
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

            _ = Task.Run(async () =>
            {
                while (!this.cancellationTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        this.listener.RecordObservableInstruments();
                    }
#pragma warning disable CA1031 // Do not catch general exception types (continue observing metrics even on error)
                    catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        this.logger.LogInformation(ex, "Exception when recording observable metrics.");
                    }

                    await Task.Delay(ObserveInterval, this.cancellationTokenSource.Token);
                }
            });
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
                    this.cancellationTokenSource.Cancel();
                    this.cancellationTokenSource.Dispose();
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
