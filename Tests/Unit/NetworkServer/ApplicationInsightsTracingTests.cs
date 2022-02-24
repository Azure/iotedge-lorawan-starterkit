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

    public sealed class ApplicationInsightsTracingTests : IDisposable
    {
        private static readonly NetworkServerConfiguration NetworkServerConfiguration = new NetworkServerConfiguration { IoTHubHostName = "somehub.azure-devices.net" };
        private readonly StubTelemetryChannel stubTelemetryChannel;
        private readonly TelemetryConfiguration configuration;
        private readonly ApplicationInsightsTracing subject;

        public ApplicationInsightsTracingTests()
        {
            this.stubTelemetryChannel = new StubTelemetryChannel();
            this.configuration = new TelemetryConfiguration
            {
                TelemetryChannel = this.stubTelemetryChannel,
                InstrumentationKey = Guid.NewGuid().ToString(),
                TelemetryInitializers = { new OperationCorrelationTelemetryInitializer() }
            };

            this.subject = new ApplicationInsightsTracing(new TelemetryClient(this.configuration), NetworkServerConfiguration);
        }

        [Fact]
        public void TrackDataMessage_Starts_ApplicationInsights_Operation()
        {
            // act
            using (var operationHolder = this.subject.TrackDataMessage()) { /* noop */ }

            // assert
            var telemetry = Assert.Single(this.stubTelemetryChannel.SentTelemetry);
            var requestTelemetry = Assert.IsType<RequestTelemetry>(telemetry);
            Assert.Equal("Data message", requestTelemetry.Name);
        }

        [Fact]
        public void TrackIotHubDependency_Starts_ApplicationInsights_Operation()
        {
            // arrange
            const string dependencyName = "SDK GetTwin";
            const string data = "id=deviceFoo";

            // act
            using (var operationHolder = this.subject.TrackIotHubDependency(dependencyName, data)) { /* noop */ }

            // assert
            var telemetry = Assert.Single(this.stubTelemetryChannel.SentTelemetry);
            var dependencyTelemetry = Assert.IsType<DependencyTelemetry>(telemetry);
            Assert.Equal("Azure IoT Hub", dependencyTelemetry.Type);
            Assert.Equal(NetworkServerConfiguration.IoTHubHostName, dependencyTelemetry.Target);
            Assert.Equal(dependencyName, dependencyTelemetry.Name);
            Assert.Equal(data, dependencyTelemetry.Data);
        }

        public void Dispose()
        {
            this.stubTelemetryChannel.Dispose();
            this.configuration.Dispose();
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
