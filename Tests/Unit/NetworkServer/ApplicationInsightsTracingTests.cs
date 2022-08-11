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

        public ApplicationInsightsTracingTests()
        {
            this.stubTelemetryChannel = new StubTelemetryChannel();
            this.configuration = new TelemetryConfiguration
            {
                TelemetryChannel = this.stubTelemetryChannel,
                ConnectionString = $"InstrumentationKey={Guid.NewGuid()};IngestionEndpoint=https://westeurope-2.in.applicationinsights.azure.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.com/",
                TelemetryInitializers = { new OperationCorrelationTelemetryInitializer() }
            };
        }

        [Fact]
        public void TrackDataMessage_Starts_ApplicationInsights_Operation()
        {
            // arrange
            var subject = Setup();

            // act
            using (var operationHolder = subject.TrackDataMessage()) { /* noop */ }

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
            var subject = Setup();

            // act
            using (var operationHolder = subject.TrackIotHubDependency(dependencyName, data)) { /* noop */ }

            // assert
            var telemetry = Assert.Single(this.stubTelemetryChannel.SentTelemetry);
            var dependencyTelemetry = Assert.IsType<DependencyTelemetry>(telemetry);
            Assert.Equal("Azure IoT Hub", dependencyTelemetry.Type);
            Assert.Equal(NetworkServerConfiguration.IoTHubHostName, dependencyTelemetry.Target);
            Assert.Equal($"{dependencyName} (Gateway)", dependencyTelemetry.Name);
            Assert.Equal(data, dependencyTelemetry.Data);
        }

        [Theory]
        [InlineData(true, "(Gateway)")]
        [InlineData(false, "(Direct)")]
        public void TrackIotHubDependency_Tracks_Gateway_Or_Direct_Mode(bool enableGateway, string expectedSuffix)
        {
            // arrange
            var networkServerConfiguration = new NetworkServerConfiguration
            {
                IoTHubHostName = NetworkServerConfiguration.IoTHubHostName,
                EnableGateway = enableGateway
            };
            var subject = Setup(networkServerConfiguration);

            // act
            using (var operationHolder = subject.TrackIotHubDependency("foo", "bar")) { /* noop */ }

            // assert
            var telemetry = Assert.Single(this.stubTelemetryChannel.SentTelemetry);
            var dependencyTelemetry = Assert.IsType<DependencyTelemetry>(telemetry);
            Assert.True(dependencyTelemetry.Name.EndsWith(expectedSuffix, StringComparison.Ordinal), $"Expected '{dependencyTelemetry.Name}' to end with '{expectedSuffix}'.");
        }

        public void Dispose()
        {
            this.stubTelemetryChannel.Dispose();
            this.configuration.Dispose();
        }

        private ApplicationInsightsTracing Setup() => Setup(NetworkServerConfiguration);

        private ApplicationInsightsTracing Setup(NetworkServerConfiguration networkServerConfiguration) =>
            new ApplicationInsightsTracing(new TelemetryClient(this.configuration), networkServerConfiguration);

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
