// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    public sealed class LoRaDeviceClientConnectionManagerTests : IDisposable
    {
        private readonly MemoryCache cache;
        private readonly LoRaDeviceClientConnectionManager subject;
        private readonly TestOutputLoggerFactory loggerFactory;
        private LoRaDevice? testDevice;

        public LoRaDeviceClientConnectionManagerTests(ITestOutputHelper testOutputHelper)
        {
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.loggerFactory = new TestOutputLoggerFactory(testOutputHelper);
            var logger = new TestOutputLogger<LoRaDeviceClientConnectionManager>(testOutputHelper);
            this.subject = new LoRaDeviceClientConnectionManager(this.cache, logger);
        }

        public void Dispose()
        {
            this.subject.Dispose();
            this.testDevice?.Dispose();
            this.cache.Dispose();
            this.loggerFactory.Dispose();
        }

        private LoRaDevice TestDevice => this.testDevice ??= new LoRaDevice(null, new DevEui(42), null);

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        public void When_Disposing_Should_Dispose_All_Managed_Connections(int numberOfDevices)
        {
            // arrange

            var deviceRegistrations =
                Enumerable.Range(0, numberOfDevices)
                          .Select(i => TestUtils.CreateFromSimulatedDevice(new SimulatedDevice(TestDeviceInfo.CreateABPDevice((uint)i)), this.subject))
                          .Select(d => (d, new Mock<ILoRaDeviceClient>()))
                          .ToList();

            foreach (var (d, c) in deviceRegistrations)
            {
                this.subject.Register(d, c.Object);
            }

            // act

            this.subject.Dispose();

            // assert

            foreach (var (_, c) in deviceRegistrations)
            {
                c.Verify(client => client.Dispose(), Times.Exactly(1));
            }
        }

        [Fact]
        public void When_Registering_Existing_Connection_Throws()
        {
            var device = TestDevice;
            this.subject.Register(device, new Mock<ILoRaDeviceClient>().Object);
            Assert.Throws<InvalidOperationException>(() => this.subject.Register(device, new Mock<ILoRaDeviceClient>().Object));
        }

        [Fact]
        public void GetClient_Returns_Registered_Client_Of_Device()
        {
            var device = TestDevice;
            this.subject.Register(device, new Mock<ILoRaDeviceClient>().Object);
            Assert.NotNull(this.subject.GetClient(device));
        }

        [Fact]
        public void GetClient_Returns_Client_That_Invokes_Underlying_Client()
        {
            // arrange

            var device = TestDevice;
            var clientMock = new Mock<ILoRaDeviceClient>();
            this.subject.Register(device, clientMock.Object);
            var client = this.subject.GetClient(device);

            // act

            client.GetTwinAsync(CancellationToken.None);

            var telemetry = new LoRaDeviceTelemetry();
            var properties = new System.Collections.Generic.Dictionary<string, string>();
            client.SendEventAsync(telemetry, properties);

            var reportedProperties = new TwinCollection();
            client.UpdateReportedPropertiesAsync(reportedProperties, CancellationToken.None);

            var timeout = TimeSpan.FromSeconds(5);
            client.ReceiveAsync(timeout);

            using var message = new Message(Stream.Null);
            client.CompleteAsync(message);
            client.AbandonAsync(message);
            client.RejectAsync(message);

            client.EnsureConnected();

            // assert

            clientMock.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Once);
            clientMock.Verify(x => x.SendEventAsync(telemetry, properties), Times.Once);
            clientMock.Verify(x => x.UpdateReportedPropertiesAsync(reportedProperties, CancellationToken.None), Times.Once);
            clientMock.Verify(x => x.ReceiveAsync(timeout), Times.Once);
            clientMock.Verify(x => x.CompleteAsync(message), Times.Once);
            clientMock.Verify(x => x.AbandonAsync(message), Times.Once);
            clientMock.Verify(x => x.RejectAsync(message), Times.Once);
            clientMock.Verify(x => x.EnsureConnected(), Times.Exactly(8));
        }

        [Fact]
        public void Client_DisconnectAsync_Does_Not_Invoke_EnsureConnected()
        {
            // arrange

            var device = TestDevice;
            var clientMock = new Mock<ILoRaDeviceClient>();
            this.subject.Register(device, clientMock.Object);
            var client = this.subject.GetClient(device);

            // act

            client.DisconnectAsync();

            // assert

            clientMock.Verify(x => x.DisconnectAsync(), Times.Once);
            clientMock.Verify(x => x.EnsureConnected(), Times.Never);
        }
    }
}
