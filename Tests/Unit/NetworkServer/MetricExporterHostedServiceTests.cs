// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using Moq;
    using Xunit;

    public class MetricExporterHostedServiceTests
    {
        [Fact]
        public async Task StartAsync_Starts_Listener()
        {
            // arrange
            var exporter = new Mock<IMetricExporter>();
            using var sut = new MetricExporterHostedService(exporter.Object);

            // act
            await sut.StartAsync(CancellationToken.None);

            // assert
            exporter.Verify(e => e.Start(), Times.Once);
        }
    }
}
