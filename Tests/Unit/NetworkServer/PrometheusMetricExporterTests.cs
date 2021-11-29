// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class PrometheusMetricExporterTests : IDisposable
    {
        private readonly Mock<Action<string, string[], double>> incCounterMock;
        private readonly Mock<Action<string, string[], double>> recordHistogramMock;
        private readonly Mock<Action<string, string[], double>> recordObservableGaugeMock;
        private readonly RegistryMetricTagBag metricTagBag;
        private readonly TestablePrometheusMetricExporter prometheusMetricExporter;
        private readonly ICollection<CustomMetric> registry;

        private CustomMetric Counter => registry.First(m => m.Type == MetricType.Counter);
        private CustomMetric Histogram => registry.First(m => m.Type == MetricType.Histogram);
        private CustomMetric ObservableGauge => registry.First(m => m.Type == MetricType.ObservableGauge);

        public PrometheusMetricExporterTests()
        {
            this.registry = new[]
            {
                new CustomMetric($"counter{Guid.NewGuid():N}", "Counter", MetricType.Counter, new[] { MetricRegistry.GatewayIdTagName }),
                new CustomMetric($"histogram{Guid.NewGuid():N}", "Histogram", MetricType.Histogram, new[] { MetricRegistry.GatewayIdTagName }),
                new CustomMetric($"observablegauge{Guid.NewGuid():N}", "Observable Gauge", MetricType.ObservableGauge, new[] { MetricRegistry.GatewayIdTagName })
            };
            this.incCounterMock = new Mock<Action<string, string[], double>>();
            this.recordHistogramMock = new Mock<Action<string, string[], double>>();
            this.recordObservableGaugeMock = new Mock<Action<string, string[], double>>();
            this.metricTagBag = new RegistryMetricTagBag();
            this.prometheusMetricExporter = new TestablePrometheusMetricExporter(this.incCounterMock.Object,
                                                                                 this.recordHistogramMock.Object,
                                                                                 this.recordObservableGaugeMock.Object,
                                                                                 this.registry.ToDictionary(m => m.Name, m => m),
                                                                                 this.metricTagBag);
        }

        public void Dispose()
        {
            this.prometheusMetricExporter.Dispose();
        }

        [Fact]
        public void When_Int_Counter_Is_Recorded_Should_Export_To_Prometheus() =>
            When_Counter_Is_Recorded_Should_Export_To_Prometheus(1, 1.0);

        [Fact]
        public void When_Byte_Counter_Is_Recorded_Should_Export_To_Prometheus() =>
            When_Counter_Is_Recorded_Should_Export_To_Prometheus((byte)0, 0);

        [Fact]
        public void When_Short_Counter_Is_Recorded_Should_Export_To_Prometheus() =>
            When_Counter_Is_Recorded_Should_Export_To_Prometheus((short)1, 1);

        [Fact]
        public void When_Long_Counter_Is_Recorded_Should_Export_To_Prometheus() =>
            When_Counter_Is_Recorded_Should_Export_To_Prometheus((long)1, 1);

        [Fact]
        public void When_Float_Counter_Is_Recorded_Should_Export_To_Prometheus() =>
            When_Counter_Is_Recorded_Should_Export_To_Prometheus((float)1.0, 1.0);

        [Fact]
        public void When_Double_Counter_Is_Recorded_Should_Export_To_Prometheus() =>
            When_Counter_Is_Recorded_Should_Export_To_Prometheus(1.0, 1.0);

        [Fact]
        public void When_Decimal_Counter_Is_Recorded_Should_Export_To_Prometheus() =>
            When_Counter_Is_Recorded_Should_Export_To_Prometheus((decimal)1, 1.0);

        [Fact]
        public void When_Double_Counter_Series_Is_Recorded_Should_Export_To_Prometheus() =>
            When_Counter_Is_Recorded_Should_Export_To_Prometheus(new[] { 1.0, 3.0 }, new[] { 1.0, 3.0 });

        private void When_Counter_Is_Recorded_Should_Export_To_Prometheus<T>(T value, double expectedReportedValue)
            where T : struct => When_Counter_Is_Recorded_Should_Export_To_Prometheus(new[] { value }, new[] { expectedReportedValue });

        private void When_Counter_Is_Recorded_Should_Export_To_Prometheus<T>(T[] values, double[] expectedReportedValues)
            where T : struct
        {
            // arrange
            const string gatewayId = "fooGateway";
            this.prometheusMetricExporter.Start();

            // act
            using var meter = new Meter("LoRaWan", "1.0");
            var counter = meter.CreateCounter<T>(Counter.Name);
            foreach (var value in values)
                counter.Add(value, KeyValuePair.Create(MetricRegistry.GatewayIdTagName, (object?)gatewayId));

            // assert
            foreach (var value in expectedReportedValues)
                this.incCounterMock.Verify(c => c.Invoke(Counter.Name, new[] { gatewayId }, value), Times.Once);
        }

        [Fact]
        public void When_Histogram_Series_Is_Recorded_Should_Export_To_Prometheus()
        {
            // arrange
            const string gatewayId = "fooGateway";
            var values = new[] { 1, 3, 10, -2 };
            this.prometheusMetricExporter.Start();

            // act
            using var meter = new Meter("LoRaWan", "1.0");
            var histogram = meter.CreateHistogram<int>(Histogram.Name);
            foreach (var value in values)
                histogram.Record(value, KeyValuePair.Create(MetricRegistry.GatewayIdTagName, (object?)gatewayId));

            // assert
            foreach (var value in values)
                this.recordHistogramMock.Verify(c => c.Invoke(Histogram.Name, new[] { gatewayId }, value), Times.Once);
        }

        [Fact]
        public async void When_ObservableGauge_Is_Recorded_Should_Export_To_Prometheus()
        {
            // arrange
            var observeValue = new Mock<Func<Measurement<int>>>();
            var stationEui = new StationEui(1);
            var measurement = new Measurement<int>(1, KeyValuePair.Create(MetricRegistry.GatewayIdTagName, (object?)stationEui));
            observeValue.Setup(ov => ov.Invoke()).Returns(measurement);
            using var meter = new Meter("LoRaWan", "1.0");
            var observableGauge = meter.CreateObservableGauge(ObservableGauge.Name, observeValue.Object);

            // act
            this.prometheusMetricExporter.Start();

            // assert
            await observeValue.RetryVerifyAsync(ov => ov.Invoke(), Times.Once);
            this.recordObservableGaugeMock.Verify(r => r.Invoke(ObservableGauge.Name, new[] { stationEui.ToString() }, measurement.Value), Times.Once);
        }

        [Theory]
        [InlineData("foo", "SomeCounter")]
        [InlineData("LoRaWan", "foo")]
        public void When_Metric_Is_Not_Known_Should_Not_Export_To_Prometheus(string @namespace, string metricId)
        {
            // arrange
            this.prometheusMetricExporter.Start();

            // act
            using var meter = new Meter(@namespace, "1.0");
            var counter = meter.CreateCounter<int>(metricId);
            counter.Add(1);

            // assert
            this.incCounterMock.Verify(c => c.Invoke(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<double>()), Times.Never);
        }

        [Fact]
        public void When_Tag_Is_Missing_Should_Fail()
        {
            // arrange
            const int value = 1;
            this.prometheusMetricExporter.Start();

            // act
            using var meter = new Meter("LoRaWan", "1.0");
            var counter = meter.CreateCounter<int>(Counter.Name);

            // assert
            Assert.Throws<LoRaProcessingException>(() => counter.Add(value));
        }

        [Fact]
        public void When_Tag_Not_Specified_Should_Fallback_To_Tag_Bag()
        {
            // arrange
            using var instrument = new Meter(MetricRegistry.Namespace, MetricRegistry.Version);
            var stationEui = new StationEui(1);
            this.metricTagBag.StationEui.Value = stationEui;
            const int value = 1;
            var counter = instrument.CreateCounter<int>(Counter.Name);

            // act
            this.prometheusMetricExporter.Start();
            counter.Add(value);

            // assert
            this.incCounterMock.Verify(me => me.Invoke(Counter.Name, new[] { stationEui.ToString() }, value), Times.Once);
        }

        private class TestablePrometheusMetricExporter : PrometheusMetricExporter
        {
            private readonly Action<string, string[], double> incCounter;
            private readonly Action<string, string[], double> observeHistogram;
            private readonly Action<string, string[], double> recordObservableGauge;

            public TestablePrometheusMetricExporter(Action<string, string[], double> incCounter,
                                                    Action<string, string[], double> recordHistogram,
                                                    Action<string, string[], double> recordObservableGauge,
                                                    IDictionary<string, CustomMetric> registryLookup,
                                                    RegistryMetricTagBag registryMetricTagBag)
                : base(registryLookup, registryMetricTagBag, NullLogger<PrometheusMetricExporter>.Instance)
            {
                this.incCounter = incCounter;
                this.observeHistogram = recordHistogram;
                this.recordObservableGauge = recordObservableGauge;
            }

            internal override void IncCounter(string metricName, string[] tags, double measurement) =>
                this.incCounter(metricName, tags, measurement);

            internal override void RecordHistogram(string metricName, string[] tags, double measurement) =>
                this.observeHistogram(metricName, tags, measurement);

            internal override void RecordObservableGauge(string metricName, string[] tags, double measurement) =>
                recordObservableGauge(metricName, tags, measurement);
        }
    }
}
