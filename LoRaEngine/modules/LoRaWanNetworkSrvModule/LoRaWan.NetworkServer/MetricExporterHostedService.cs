// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    internal sealed class MetricExporterHostedService : IHostedService, IDisposable
    {
        private readonly IMetricExporter metricExporter;

        public MetricExporterHostedService(IMetricExporter metricExporter)
        {
            this.metricExporter = metricExporter;
        }

        public void Dispose()
        {
            this.metricExporter.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.metricExporter.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
