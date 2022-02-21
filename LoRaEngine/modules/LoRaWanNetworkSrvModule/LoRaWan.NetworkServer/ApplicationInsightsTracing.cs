// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;

    public interface ITracing
    {
        IDisposable TrackDataMessage();
    }

    internal sealed class ApplicationInsightsTracing : ITracing
    {
        private readonly TelemetryClient telemetryClient;

        public ApplicationInsightsTracing(TelemetryClient telemetryClient) =>
            this.telemetryClient = telemetryClient;

        public IDisposable TrackDataMessage() => this.telemetryClient.StartOperation<RequestTelemetry>("Data message");
    }

    internal sealed class NoopTracing : ITracing
    {
        public IDisposable TrackDataMessage() => NoopDisposable.Instance;
    }
}
