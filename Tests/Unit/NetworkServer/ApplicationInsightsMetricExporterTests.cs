// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using LoRaWan.NetworkServer;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Extensibility;
    using Moq;
    using Xunit;

    public sealed class ApplicationInsightsMetricExporterTests : IDisposable
    {
        private const string ExistingMetricName = "SomeCounter";

        private readonly TelemetryConfiguration telemetryConfiguration;
        private readonly Mock<Action<Metric, object, string[]>> trackValueMock;

        private readonly ApplicationInsightsMetricExporter applicationInsightsMetricExporter;

        public ApplicationInsightsMetricExporterTests()
        {
            this.telemetryConfiguration = new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object };
            this.trackValueMock = new Mock<Action<Metric, object, string[]>>();
            this.applicationInsightsMetricExporter = new TestableApplicationInsightsExporter(new TelemetryClient(this.telemetryConfiguration), this.trackValueMock.Object);
        }

        [Theory]
        [InlineData(ExistingMetricName)]
        public void When_Metric_Raised_Exports_Supported_Int_Metrics(string metricId) =>
            ApplicationInsights_Metrics_Collection_Raises_Supported_Metrics(metricId, 5);

        private void ApplicationInsights_Metrics_Collection_Raises_Supported_Metrics<T>(string metricId, T metricValue)
            where T : struct
        {
            // arrange
            const string gateway = "foogateway";
            using var meter = new Meter("LoRaWan", "1.0");
            var counter = meter.CreateCounter<T>(metricId);

            // act
            applicationInsightsMetricExporter.Start();
            counter.Add(metricValue, KeyValuePair.Create(MetricsExporter.GatewayIdTagName, (object)gateway));

            // assert
            this.trackValueMock.Verify(me => me.Invoke(It.Is<Metric>(m => m.Identifier.MetricNamespace == MetricsExporter.Namespace
                                                                          && m.Identifier.MetricId == metricId),
                                                                     metricValue,
                                                                     new[] { gateway }),
                                                       Times.Once);
        }

        [Theory]
        [InlineData("LoRaWan", "foometric")]
        [InlineData("foo", ExistingMetricName)]
        public void When_Raising_Unknown_Metric_Does_Not_Export_To_Application_Insights(string @namespace, string metricName)
        {
            // arrange
            using var instrument = new Meter(@namespace, MetricsExporter.MetricsVersion);
            var counter = instrument.CreateCounter<int>(metricName);

            // act
            applicationInsightsMetricExporter.Start();
            counter.Add(1);

            // assert
            this.trackValueMock.Verify(me => me.Invoke(It.IsAny<Metric>(), 1, It.IsAny<string[]>()), Times.Never);
        }

        [Fact]
        public void When_Raising_Metric_And_Missing_Dimensions_Should_Report_Empty_String()
        {
            // arrange
            using var instrument = new Meter(MetricsExporter.Namespace, MetricsExporter.MetricsVersion);
            var counter = instrument.CreateCounter<int>(ExistingMetricName);

            // act
            applicationInsightsMetricExporter.Start();
            counter.Add(1);

            // assert
            this.trackValueMock.Verify(me => me.Invoke(It.IsAny<Metric>(), 1, new[] { string.Empty }), Times.Once);
        }

        [Fact]
        public void When_Raising_Metric_Dimensions_Should_Be_Case_Insensitive()
        {
            // arrange
            const string gateway = "foogateway";
            using var instrument = new Meter(MetricsExporter.Namespace, MetricsExporter.MetricsVersion);
            var counter = instrument.CreateCounter<int>(ExistingMetricName);

            // act
            applicationInsightsMetricExporter.Start();
            counter.Add(1, new KeyValuePair<string, object>(MetricsExporter.GatewayIdTagName.ToUpperInvariant(), gateway));

            // assert
            this.trackValueMock.Verify(me => me.Invoke(It.IsAny<Metric>(), 1, new[] { gateway }), Times.Once);
        }

        public void Dispose()
        {
            this.telemetryConfiguration.Dispose();
            this.applicationInsightsMetricExporter.Dispose();
        }

        private sealed class TestableApplicationInsightsExporter : ApplicationInsightsMetricExporter
        {
            private readonly Action<Metric, object, string[]> trackValue;

            public TestableApplicationInsightsExporter(TelemetryClient telemetryClient, Action<Metric, object, string[]> trackValue)
                : base(telemetryClient)
            {
                this.trackValue = trackValue;
            }

            internal override void TrackValue<T>(Metric metric, T measurement, params string[] dimensions) =>
                this.trackValue(metric, measurement, dimensions);
        }
    }
}
