// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using LoRaWan.NetworkServer;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    /// <summary>
    /// Tests the client returned by <see cref="LoRaDeviceClientExtensions.AddResiliency"/>.
    /// </summary>
    public sealed class LoRaDeviceClientResiliencyTests : IAsyncDisposable
    {
        private readonly Mock<ILoRaDeviceClient> originalMock;
        private readonly Mock<ILogger> loggerMock = new();
        private readonly ILoRaDeviceClient subject;
        private readonly CancellationTokenSource cancellationTokenSource = new();

        public LoRaDeviceClientResiliencyTests()
        {
            this.originalMock = new Mock<ILoRaDeviceClient>();
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(this.loggerMock.Object);
            this.subject = this.originalMock.Object.AddResiliency(loggerFactoryMock.Object);
        }

        public async ValueTask DisposeAsync()
        {
            this.cancellationTokenSource.Dispose();
            await this.subject.DisposeAsync();
        }

        private CancellationToken CancellationToken => this.cancellationTokenSource.Token;

        public static readonly TheoryData<Exception> RetriedExceptions =
            TheoryDataFactory.From<Exception>(new InvalidOperationException("This operation is only allowed using a successfully authenticated context. " + "" +
                                                                            "This sentence in the error message shouldn't matter."),
                                              new ObjectDisposedException("<object>"));

        [Fact]
        public async Task GetTwinAsync_Invokes_Original_Client()
        {
            var twin = new Twin();
            this.originalMock.Setup(x => x.GetTwinAsync(CancellationToken)).ReturnsAsync(twin);

            var result = await this.subject.GetTwinAsync(CancellationToken);

            Assert.Same(twin, result);
        }

        [Fact]
        public async Task SendEventAsync_Invokes_Original_Client()
        {
            var telemetry = new LoRaDeviceTelemetry();
            var properties = new Dictionary<string, string>();
            this.originalMock.Setup(x => x.SendEventAsync(telemetry, properties)).ReturnsAsync(true);

            var result = await this.subject.SendEventAsync(telemetry, properties);

            Assert.True(result);
        }

        [Fact]
        public async Task UpdateReportedPropertiesAsync_Invokes_Original_Client()
        {
            var properties = new TwinCollection();
            this.originalMock.Setup(x => x.UpdateReportedPropertiesAsync(properties, CancellationToken)).ReturnsAsync(true);

            var result = await this.subject.UpdateReportedPropertiesAsync(properties, CancellationToken);

            Assert.True(result);
        }

        [Fact]
        public async Task ReceiveAsync_Invokes_Original_Client()
        {
            var timeout = TimeSpan.FromSeconds(5);
            using var message = new Message();
            this.originalMock.Setup(x => x.ReceiveAsync(timeout)).ReturnsAsync(message);

            var result = await this.subject.ReceiveAsync(timeout);

            Assert.Same(message, result);
        }

        [Fact]
        public async Task CompleteAsync_Invokes_Original_Client()
        {
            using var message = new Message();
            this.originalMock.Setup(x => x.CompleteAsync(message)).ReturnsAsync(true);

            var result = await this.subject.CompleteAsync(message);

            Assert.True(result);
        }

        [Fact]
        public async Task AbandonAsync_Invokes_Original_Client()
        {
            using var message = new Message();
            this.originalMock.Setup(x => x.AbandonAsync(message)).ReturnsAsync(true);

            var result = await this.subject.AbandonAsync(message);

            Assert.True(result);
        }

        [Fact]
        public async Task RejectAsync_Invokes_Original_Client()
        {
            using var message = new Message();
            this.originalMock.Setup(x => x.RejectAsync(message)).ReturnsAsync(true);

            var result = await this.subject.RejectAsync(message);

            Assert.True(result);
        }

        [Fact]
        public void EnsureConnected_Invokes_Original_Client()
        {
            this.originalMock.Setup(x => x.EnsureConnected()).Returns(true);

            var result = this.subject.EnsureConnected();

            Assert.True(result);
        }

        [Theory]
        [MemberData(nameof(RetriedExceptions))]
        public void EnsureConnected_Is_Not_Resilient(Exception exception)
        {
            this.originalMock.Setup(x => x.EnsureConnected()).Throws(exception);

            var ex = Assert.Throws(exception.GetType(), () => _ = this.subject.EnsureConnected());

            Assert.Same(exception, ex);
            this.originalMock.Verify(x => x.EnsureConnected(), Times.Once);
        }

        [Fact]
        public async Task DisconnectAsync_Invokes_Original_Client()
        {
            await this.subject.DisconnectAsync(CancellationToken);

            this.originalMock.Verify(x => x.DisconnectAsync(CancellationToken), Times.Once);
        }

        [Theory]
        [MemberData(nameof(RetriedExceptions))]
        public async Task DisconnectAsync_Is_Not_Resilient(Exception exception)
        {
            this.originalMock.Setup(x => x.DisconnectAsync(CancellationToken)).Throws(exception);

            var ex = await Assert.ThrowsAsync(exception.GetType(), () => this.subject.DisconnectAsync(CancellationToken));

            Assert.Same(exception, ex);
            this.originalMock.Verify(x => x.DisconnectAsync(CancellationToken), Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_Invokes_Original_Client()
        {
            await this.subject.DisposeAsync();

            this.originalMock.Verify(x => x.DisposeAsync(), Times.Once);
        }

        [Theory]
        [MemberData(nameof(RetriedExceptions))]
        public async Task DisposeAsync_Is_Not_Resilient(Exception exception)
        {
            this.originalMock.Setup(x => x.DisposeAsync()).Throws(exception);

            var ex = await Assert.ThrowsAsync(exception.GetType(), () => this.subject.DisposeAsync().AsTask());

            Assert.Same(exception, ex);
            this.originalMock.Verify(x => x.DisposeAsync(), Times.Once);
        }
    }
}
