// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using LoRaWan.NetworkServer;
    using Moq;
    using Xunit;

    public sealed class MetricRegistryTests : IDisposable
    {
        private readonly Meter meter;

        public MetricRegistryTests()
        {
            this.meter = new Meter(MetricRegistry.Namespace, MetricRegistry.Version);
        }

        [Fact]
        public void CustomMetric_Field_Must_Be_Registered_In_Registry()
        {
            var customMetricFields = typeof(MetricRegistry).GetFields().Where(f => f.FieldType == typeof(CustomMetric)).Select(f => f.Name).ToList();
            Assert.Equal(customMetricFields.Count, customMetricFields.Intersect(MetricRegistry.RegistryLookup.Keys).Count());
        }

        [Fact]
        public void CreateCounter_Success_Case()
        {
            // arrange
            var customMetric = new CustomMetric("foo", "bar", MetricType.Counter, Array.Empty<string>());

            // act
            var result = this.meter.CreateCounter<int>(customMetric);

            // assert
            Assert.Equal(customMetric.Name, result.Name);
            Assert.Equal(customMetric.Description, result.Description);
        }

        [Fact]
        public void CreateCounter_Throws_When_Argument_Is_Histogram()
        {
            // arrange
            var customMetric = new CustomMetric("foo", "bar", MetricType.Histogram, Array.Empty<string>());

            // act + assert
            Assert.Throws<ArgumentException>(() => this.meter.CreateCounter<int>(customMetric));
        }

        [Fact]
        public void CreateHistogram_Success_Case()
        {
            // arrange
            var customMetric = new CustomMetric("foo", "bar", MetricType.Histogram, Array.Empty<string>());

            // act
            var result = this.meter.CreateHistogram<int>(customMetric);

            // assert
            Assert.Equal(customMetric.Name, result.Name);
            Assert.Equal(customMetric.Description, result.Description);
        }

        [Fact]
        public void CreateHistogram_Throws_When_Argument_Is_Counter()
        {
            // arrange
            var customMetric = new CustomMetric("foo", "bar", MetricType.Counter, Array.Empty<string>());

            // act + assert
            Assert.Throws<ArgumentException>(() => this.meter.CreateHistogram<int>(customMetric));
        }

        [Fact]
        public void GetTagsInOrder_Returns_Tags_When_Invoked_With_Ordered_Tags()
        {
            // arrange
            var tags = new[] { "foo", "bar" };
            var tagValues = new[] { KeyValuePair.Create("foo", (object?)"foovalue"), KeyValuePair.Create("bar", (object?)"barvalue") };

            // act
            var result = MetricExporterHelper.GetTagsInOrder(tags, tagValues);

            // assert
            Assert.Equal(new[] { "foovalue", "barvalue" }, result);
        }

        [Fact]
        public void GetTagsInOrder_Returns_Tags_When_Invoked_With_Unordered_Tags()
        {
            // arrange
            var tags = new[] { "foo", "bar" };
            var tagValues = new[] { KeyValuePair.Create("bar", (object?)"barvalue"), KeyValuePair.Create("foo", (object?)"foovalue") };

            // act
            var result = MetricExporterHelper.GetTagsInOrder(tags, tagValues);

            // assert
            Assert.Equal(new[] { "foovalue", "barvalue" }, result);
        }

        [Fact]
        public void GetTagsInOrder_Throws_When_Tag_Not_Found()
        {
            var result = Assert.Throws<LoRaProcessingException>(() => MetricExporterHelper.GetTagsInOrder(new[] { "foo" }, Array.Empty<KeyValuePair<string, object?>>()));
            Assert.Equal(LoRaProcessingErrorCode.TagNotSet, result.ErrorCode);
        }

        [Fact]
        public void GetTagsInOrder_Throws_When_Tag_Is_Empty()
        {
            const string tagName = "foo";
            var result = Assert.Throws<LoRaProcessingException>(() => MetricExporterHelper.GetTagsInOrder(new[] { tagName }, new[] { KeyValuePair.Create(tagName, (object?)string.Empty) }));
            Assert.Equal(LoRaProcessingErrorCode.TagNotSet, result.ErrorCode);
        }

        [Fact]
        public void CompositeMetricExporter_Works_If_One_Exporter_Null()
        {
            // arrange
            var metricExporter = new Mock<IMetricExporter>();

            // act
            using var first = new CompositeMetricExporter(null, metricExporter.Object);
            using var second = new CompositeMetricExporter(metricExporter.Object, null);
            first.Start();
            second.Start();

            // assert
            metricExporter.Verify(me => me.Start(), Times.Exactly(2));
        }

        [Fact]
        public void CompositeMetricExporter_Works_If_Both_Exporters_Defined()
        {
            // arrange
            var firstExporter = new Mock<IMetricExporter>();
            var secondExporter = new Mock<IMetricExporter>();

            // act
            using var result = new CompositeMetricExporter(firstExporter.Object, secondExporter.Object);
            result.Start();

            // assert
            firstExporter.Verify(me => me.Start(), Times.Once);
            secondExporter.Verify(me => me.Start(), Times.Once);
        }

        public void Dispose()
        {
            this.meter.Dispose();
        }
    }
}
