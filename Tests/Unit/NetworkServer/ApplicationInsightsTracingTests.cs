// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using LoRaWan.NetworkServer;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Xunit;

    public sealed class ApplicationInsightsTracingTests
    {
        [Fact]
        public void TrackDataMessage_Starts_ApplicationInsights_Operation()
        {
            // arrange
            var stub = new StubTelemetryChannel();
            using var configuration = new TelemetryConfiguration
            {
                TelemetryChannel = stub,
                InstrumentationKey = Guid.NewGuid().ToString(),
                TelemetryInitializers = { new OperationCorrelationTelemetryInitializer() }
            };
            var subject = new ApplicationInsightsTracing(new TelemetryClient(configuration));

            // act
            using (var operationHolder = subject.TrackDataMessage()) { /* noop */ }

            // assert
            var telemetry = Assert.Single(stub.SentTelemetry);
            var requestTelemetry = Assert.IsType<RequestTelemetry>(telemetry);
            Assert.Equal("Data message", requestTelemetry.Name);
        }

        private sealed class StubTelemetryChannel : ITelemetryChannel
        {
            private readonly List<ITelemetry> sentTelemetry = new List<ITelemetry>();

            public IReadOnlyList<ITelemetry> SentTelemetry => this.sentTelemetry;

            public bool? DeveloperMode { get; set; } = false;
            public string EndpointAddress { get; set; } = "https://sometesturi.ms";

            public void Dispose() { }

            public void Flush() { }

            public void Send(ITelemetry item) => this.sentTelemetry.Add(item);
        }
    }
}
