// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using LoRaWan.NetworkServer;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Extensibility;
    using Moq;
    using Xunit;

    public sealed class ApplicationInsightsMetricExporterTests : IDisposable
    {
        private readonly CustomMetric[] registry;
        private readonly TelemetryConfiguration telemetryConfiguration;
        private readonly Mock<Action<Metric, double, string[]>> trackValueMock;
        private readonly ApplicationInsightsMetricExporter applicationInsightsMetricExporter;
        private CustomMetric CounterMetric => this.registry.First(m => m.Type == MetricType.Counter);

        public ApplicationInsightsMetricExporterTests()
        {
            this.registry = new[]
            {
                new CustomMetric(Guid.NewGuid().ToString(), "Counter", MetricType.Counter, new[] { MetricRegistry.GatewayIdTagName }),
                new CustomMetric(Guid.NewGuid().ToString(), "Histogram", MetricType.Histogram, new[] { MetricRegistry.GatewayIdTagName })
            };
            this.telemetryConfiguration = new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object };
            this.trackValueMock = new Mock<Action<Metric, double, string[]>>();
            this.applicationInsightsMetricExporter = new TestableApplicationInsightsExporter(new TelemetryClient(this.telemetryConfiguration),
                                                                                             this.trackValueMock.Object,
                                                                                             this.registry.ToDictionary(m => m.Name, m => m));
        }

        [Fact]
        public void When_Metric_Raised_Exports_Supported_Int_Counter() =>
            ApplicationInsights_Metrics_Collection_Raises_Counter_Metrics(5, 5.0);

        [Fact]
        public void When_Metric_Raised_Exports_Supported_Byte_Counter() =>
            ApplicationInsights_Metrics_Collection_Raises_Counter_Metrics((byte)0, 0);

        [Fact]
        public void When_Metric_Raised_Exports_Supported_Short_Counter() =>
            ApplicationInsights_Metrics_Collection_Raises_Counter_Metrics((short)5, 5);

        [Fact]
        public void When_Metric_Raised_Exports_Supported_Long_Counter() =>
            ApplicationInsights_Metrics_Collection_Raises_Counter_Metrics((long)5, 5);

        [Fact]
        public void When_Metric_Raised_Exports_Supported_Float_Counter() =>
            ApplicationInsights_Metrics_Collection_Raises_Counter_Metrics((float)5, 5.0);

        [Fact]
        public void When_Metric_Raised_Exports_Supported_Double_Counter() =>
            ApplicationInsights_Metrics_Collection_Raises_Counter_Metrics(5.0, 5.0);

        [Fact]
        public void When_Metric_Raised_Exports_Supported_Decimal_Counter() =>
            ApplicationInsights_Metrics_Collection_Raises_Counter_Metrics((decimal)5.0, 5.0);

        [Fact]
        public void When_Metric_Raised_Exports_Int_Counter_Series() =>
            ApplicationInsights_Metrics_Collection_Raises_Counter_Metrics(new[] { 5, 6, 1 }, new[] { 5.0, 6.0, 1.0 });

        private void ApplicationInsights_Metrics_Collection_Raises_Counter_Metrics<T>(T metricValue, double expectedReportedValue)
            where T : struct =>
            ApplicationInsights_Metrics_Collection_Raises_Counter_Metrics(new[] { metricValue }, new[] { expectedReportedValue });

        private void ApplicationInsights_Metrics_Collection_Raises_Counter_Metrics<T>(T[] metricValues, double[] expectedReportedValues)
            where T : struct
        {
            // arrange
            const string gateway = "foogateway";
            using var meter = new Meter("LoRaWan", "1.0");
            var counter = meter.CreateCounter<T>(CounterMetric.Name);

            // act
            applicationInsightsMetricExporter.Start();
            foreach (var val in metricValues)
                counter.Add(val, KeyValuePair.Create(MetricRegistry.GatewayIdTagName, (object)gateway));

            // assert
            foreach (var expectedReportedValue in expectedReportedValues)
            {
                this.trackValueMock.Verify(me => me.Invoke(It.Is<Metric>(m => m.Identifier.MetricNamespace == MetricRegistry.Namespace
                                                                              && m.Identifier.MetricId == CounterMetric.Name),
                                                           expectedReportedValue,
                                                           new[] { gateway }),
                                           Times.Once);
            }

        }

        [Theory]
        [InlineData("LoRaWan", "foometric")]
        [InlineData("foo", "SomeCounter")]
        public void When_Raising_Unknown_Metric_Does_Not_Export_To_Application_Insights(string @namespace, string metricName)
        {
            // arrange
            using var instrument = new Meter(@namespace, MetricRegistry.MetricsVersion);
            var counter = instrument.CreateCounter<int>(metricName);

            // act
            applicationInsightsMetricExporter.Start();
            counter.Add(1);

            // assert
            this.trackValueMock.Verify(me => me.Invoke(It.IsAny<Metric>(), It.IsAny<double>(), It.IsAny<string[]>()), Times.Never);
        }

        [Fact]
        public void When_Raising_Metric_And_Missing_Dimensions_Should_Report_Empty_String()
        {
            // arrange
            using var instrument = new Meter(MetricRegistry.Namespace, MetricRegistry.MetricsVersion);
            var counter = instrument.CreateCounter<int>(CounterMetric.Name);

            // act
            applicationInsightsMetricExporter.Start();
            counter.Add(1);

            // assert
            this.trackValueMock.Verify(me => me.Invoke(It.IsAny<Metric>(), It.IsAny<double>(), new[] { string.Empty }), Times.Once);
        }

        [Fact]
        public void When_Raising_Metric_Dimensions_Should_Be_Case_Insensitive()
        {
            // arrange
            const string gateway = "foogateway";
            using var meter = new Meter(MetricRegistry.Namespace, MetricRegistry.MetricsVersion);
            var counter = meter.CreateCounter<int>(CounterMetric.Name);

            // act
            applicationInsightsMetricExporter.Start();
            counter.Add(1, new KeyValuePair<string, object>(MetricRegistry.GatewayIdTagName.ToUpperInvariant(), gateway));

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
            private readonly Action<Metric, double, string[]> trackValue;

            public TestableApplicationInsightsExporter(TelemetryClient telemetryClient,
                                                       Action<Metric, double, string[]> trackValue,
                                                       IDictionary<string, CustomMetric> registryLookup)
                : base(telemetryClient, registryLookup)
            {
                this.trackValue = trackValue;
            }

            internal override void TrackValue(Metric metric, double measurement, params string[] dimensions) =>
                this.trackValue(metric, measurement, dimensions);
        }
    }
}
