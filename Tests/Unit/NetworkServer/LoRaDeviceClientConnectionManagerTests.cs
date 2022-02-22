// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    public sealed class LoRaDeviceClientConnectionManagerTests : IAsyncDisposable
    {
        private readonly Mock<IMemoryCache> cacheMock;
        private readonly LoRaDeviceClientConnectionManager subject;
        private readonly TestOutputLoggerFactory loggerFactory;
        private readonly ILogger<LoRaDeviceClientConnectionManager> logger;
        private LoRaDevice? testDevice;

        public LoRaDeviceClientConnectionManagerTests(ITestOutputHelper testOutputHelper)
        {
            this.cacheMock = new Mock<IMemoryCache>();
            var cache = this.cacheMock.Object;
            this.loggerFactory = new TestOutputLoggerFactory(testOutputHelper);
            this.logger = new TestOutputLogger<LoRaDeviceClientConnectionManager>(testOutputHelper);
            this.subject = new LoRaDeviceClientConnectionManager(cache, this.loggerFactory, this.logger);
        }

        public async ValueTask DisposeAsync()
        {
            await this.subject.DisposeAsync();
            if (this.testDevice is { } someTestDevice)
                await someTestDevice.DisposeAsync();
            this.loggerFactory.Dispose();
        }

        private LoRaDevice TestDevice => this.testDevice ??= new(null, new DevEui(42), this.subject);

        private (Mock<ILoRaDeviceClient>, ILoRaDeviceClient) RegisterTestDevice(TimeSpan? keepAliveTimeout = null)
        {
            var device = TestDevice;
            device.KeepAliveTimeout = (int)(keepAliveTimeout ?? TimeSpan.Zero).TotalSeconds;
            var mock = new Mock<ILoRaDeviceClient>();
            this.subject.Register(device, mock.Object);
            mock.Setup(x => x.EnsureConnected()).Returns(true);
            return (mock, this.subject.GetClient(device));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        public async Task When_Disposing_Should_Dispose_All_Managed_Connections(int numberOfDevices)
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

            await this.subject.DisposeAsync();

            // assert

            foreach (var (_, c) in deviceRegistrations)
            {
                c.Verify(client => client.DisposeAsync(), Times.Exactly(1));
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

            var (clientMock, client) = RegisterTestDevice();

            // act

            client.GetTwinAsync(CancellationToken.None);

            var telemetry = new LoRaDeviceTelemetry();
            var properties = new Dictionary<string, string>();
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
        public async Task Client_DisconnectAsync_Does_Not_Invoke_EnsureConnected()
        {
            // arrange

            var (clientMock, client) = RegisterTestDevice();

            // act

            await client.DisconnectAsync(CancellationToken.None);

            // assert

            clientMock.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Once);
            clientMock.Verify(x => x.EnsureConnected(), Times.Never);
        }

        private Mock<ICacheEntry> CreateCacheEntryMock(IList<PostEvictionCallbackRegistration>? postEvictionCallbackRegistrationList = null)
        {
            var cacheEntryMock = new Mock<ICacheEntry>();
            this.cacheMock.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(cacheEntryMock.Object);
            cacheEntryMock.SetupAllProperties();
            if (postEvictionCallbackRegistrationList is { } somePostEvictionCallbackRegistrationList)
                cacheEntryMock.Setup(x => x.PostEvictionCallbacks).Returns(somePostEvictionCallbackRegistrationList);
            return cacheEntryMock;
        }

        [Fact]
        public async Task Client_With_Zero_KeepAliveTimeout_Is_Never_Cached_For_Disconnection()
        {
            // arrange

            var postEvictionCallbackRegistrationList = new List<PostEvictionCallbackRegistration>();
            _ = CreateCacheEntryMock(postEvictionCallbackRegistrationList);

            var (clientMock, client) = RegisterTestDevice(keepAliveTimeout: TimeSpan.Zero);
            clientMock.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(new Twin());

            // 1st act

            await client.GetTwinAsync(CancellationToken.None);

            // assert

            clientMock.Verify(x => x.EnsureConnected(), Times.Once);
            clientMock.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Never);
            Assert.Empty(postEvictionCallbackRegistrationList);
        }

        [Fact]
        public async Task Client_With_Non_Zero_KeepAliveTimeout_Is_Disconnected_When_Evicted_From_Cache()
        {
            // arrange

            var postEvictionCallbackRegistrationList = new List<PostEvictionCallbackRegistration>();
            var cacheEntryMock = CreateCacheEntryMock(postEvictionCallbackRegistrationList);

            var (clientMock, client) = RegisterTestDevice(keepAliveTimeout: TimeSpan.FromSeconds(1));
            clientMock.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(new Twin());

            // 1st act

            await client.GetTwinAsync(CancellationToken.None);

            // assert

            clientMock.Verify(x => x.EnsureConnected(), Times.Once);
            var registration = Assert.Single(postEvictionCallbackRegistrationList);

            // 2nd act

            registration.EvictionCallback(null, cacheEntryMock.Object.Value, EvictionReason.Expired, registration.State);

            // assert

            clientMock.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task Client_Disconnection_Is_Deferred_When_An_Activity_Is_Outstanding()
        {
            // arrange

            _ = CreateCacheEntryMock(new List<PostEvictionCallbackRegistration>());

            var (clientMock, client) = RegisterTestDevice(keepAliveTimeout: TimeSpan.FromSeconds(1));
            clientMock.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(new Twin());

            // 1st act

            await using (TestDevice.BeginDeviceClientConnectionActivity())
            {
                await client.GetTwinAsync(CancellationToken.None);

                // assert

                clientMock.Verify(x => x.EnsureConnected(), Times.Once);

                // 2nd act

                await client.DisconnectAsync(CancellationToken.None);

                // assert

                clientMock.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Never);

                // 3rd act (activity disposed)
            }

            // assert

            clientMock.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task Client_Disconnection_Is_Deferred_When_Several_Activities_Are_Outstanding()
        {
            // arrange

            _ = CreateCacheEntryMock(new List<PostEvictionCallbackRegistration>());

            var (clientMock, client) = RegisterTestDevice(keepAliveTimeout: TimeSpan.FromSeconds(1));
            clientMock.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(new Twin());

            // act

            await using (TestDevice.BeginDeviceClientConnectionActivity())
            await using (TestDevice.BeginDeviceClientConnectionActivity())
            await using (TestDevice.BeginDeviceClientConnectionActivity())
            {
                await client.GetTwinAsync(CancellationToken.None);
                await client.DisconnectAsync(CancellationToken.None);

                // assert

                clientMock.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Never);
            }

            clientMock.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task Client_Not_Disconnected_If_No_Disconnection_Deferred_During_Activities()
        {
            // arrange

            _ = CreateCacheEntryMock(new List<PostEvictionCallbackRegistration>());

            var (clientMock, client) = RegisterTestDevice(keepAliveTimeout: TimeSpan.FromSeconds(1));
            clientMock.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(new Twin());

            // act

            await using (TestDevice.BeginDeviceClientConnectionActivity())
            await using (TestDevice.BeginDeviceClientConnectionActivity())
            await using (TestDevice.BeginDeviceClientConnectionActivity())
            {
                await client.GetTwinAsync(CancellationToken.None);
            }

            // assert

            clientMock.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Never);
        }

        [Fact]
        public async Task Client_Disconnection_Via_KeepAliveTimeout_Expiry_Is_Deferred_When_An_Activity_Is_Outstanding()
        {
            // arrange

            var postEvictionCallbackRegistrationList = new List<PostEvictionCallbackRegistration>();
            var cacheEntryMock = CreateCacheEntryMock(postEvictionCallbackRegistrationList);

            var (clientMock, client) = RegisterTestDevice(keepAliveTimeout: TimeSpan.FromSeconds(1));
            clientMock.Setup(x => x.GetTwinAsync(CancellationToken.None)).ReturnsAsync(new Twin());

            // 1st act

            await using (TestDevice.BeginDeviceClientConnectionActivity())
            {
                await client.GetTwinAsync(CancellationToken.None);

                // assert

                var registration = Assert.Single(postEvictionCallbackRegistrationList);

                // 2nd act

                registration.EvictionCallback(null, cacheEntryMock.Object.Value, EvictionReason.Expired, registration.State);

                // assert

                clientMock.Verify(x => x.EnsureConnected(), Times.Once);
                clientMock.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Never);

                // 3rd act (activity disposed)
            }

            // assert

            clientMock.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task Client_Operations_Are_Synchronized()
        {
            var (clientMock, client) = RegisterTestDevice();

            // Monitor operations on the client.

            var queuedOperations = new List<(int Id, string Name)>();
            var completedOperations = new List<(int Id, string Name)>();

            var eventSource = (ILoRaDeviceClientSynchronizedOperationEventSource)client;
            eventSource.Queued += (_, args) => queuedOperations.Add((args.Id, args.Name));
            eventSource.Processed += (_, args) => completedOperations.Add((args.Id, args.Name));

            // Setup the client mock.

            var tcs = new
            {
                GetTwinAsync = new TaskCompletionSource<Twin>(),
                UpdateReportedPropertiesAsync = new TaskCompletionSource<bool>(),
                DisconnectAsync = new TaskCompletionSource(),
            };

            clientMock.Setup(x => x.GetTwinAsync(CancellationToken.None)).Returns(tcs.GetTwinAsync.Task);

            var reportedProperties = new TwinCollection();
            clientMock.Setup(x => x.UpdateReportedPropertiesAsync(reportedProperties, CancellationToken.None)).Returns(tcs.UpdateReportedPropertiesAsync.Task);

            clientMock.Setup(x => x.DisconnectAsync(CancellationToken.None)).Returns(tcs.DisconnectAsync.Task);

            // Issue the first operation and...

            var getTwinTask = client.GetTwinAsync(CancellationToken.None);

            // ...assert state of each operations list is the expected:

            Assert.Single(queuedOperations);
            Assert.Empty(completedOperations);

            // Issue the second operations and...

            var updateReportedPropertiesTask = client.UpdateReportedPropertiesAsync(reportedProperties, CancellationToken.None);

            // ...assert state of each operations list is the expected:

            Assert.Equal(2, queuedOperations.Count);
            Assert.Empty(completedOperations);

            // Issue the third operation and...

            var disconnectTask = client.DisconnectAsync(CancellationToken.None);

            // ...assert state of each operations list is the expected:

            Assert.Equal(3, queuedOperations.Count);
            Assert.Empty(completedOperations);

            // Assert that the queued operations are the expected ones and in the expected order.

            var expectedQueuedOperations = new[]
            {
                (1, nameof(client.GetTwinAsync)),
                (2, nameof(client.UpdateReportedPropertiesAsync)),
                (3, nameof(client.DisconnectAsync))
            };
            Assert.Equal(expectedQueuedOperations, queuedOperations);

            // Now complete all operations.

            tcs.GetTwinAsync.SetResult(new Twin());
            tcs.UpdateReportedPropertiesAsync.SetResult(true);
            tcs.DisconnectAsync.SetResult();

            await Task.WhenAll(getTwinTask, updateReportedPropertiesTask, disconnectTask);

            // Assert the the queued operations completed in the same order.

            Assert.Equal(completedOperations, queuedOperations);
        }
    }
}
