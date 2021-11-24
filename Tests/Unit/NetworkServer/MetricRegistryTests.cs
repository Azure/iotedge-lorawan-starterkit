// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Diagnostics.Metrics;
    using LoRaWan.NetworkServer;
    using Xunit;

    public sealed class MetricRegistryTests : IDisposable
    {
        private readonly Meter meter;

        public MetricRegistryTests()
        {
            this.meter = new Meter(MetricRegistry.Namespace, MetricRegistry.Version);
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

        public void Dispose()
        {
            this.meter.Dispose();
        }
    }
}
