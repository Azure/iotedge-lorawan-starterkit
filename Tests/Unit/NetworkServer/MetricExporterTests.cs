// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using LoRaWan.NetworkServer;
    using Moq;
    using Xunit;

    public class MetricExporterTests
    {
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
        public void GetTagsInOrder_Returns_Empty_String_When_Tag_Not_Found()
        {
            // arrange + act
            var result = MetricExporterHelper.GetTagsInOrder(new[] { "foo" }, Array.Empty<KeyValuePair<string, object?>>());

            // assert
            Assert.Equal(new[] { string.Empty }, result);
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
    }
}
