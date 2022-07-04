// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Moq.Protected;
    using Xunit;

    public class LoRaDeviceCacheTest
    {
        [Fact]
        public async Task When_Device_Expires_It_Is_Refreshed()
        {
            var moqCallback = new Mock<Action<LoRaDevice>>();
            await using var cache = new TestDeviceCache(moqCallback.Object, this.quickRefreshOptions, true);
            var deviceMock = CreateMockDevice();
            var disposableMock = new Mock<IAsyncDisposable>();
            deviceMock.Setup(x => x.BeginDeviceClientConnectionActivity())
                      .Returns(disposableMock.Object);
            var device = deviceMock.Object;
            cache.Register(device);
            await cache.WaitForRefreshAsync(CancellationToken.None);
            moqCallback.Verify(x => x.Invoke(device));
            disposableMock.Verify(x => x.DisposeAsync(), Times.Once);
        }

        [Fact]
        public async Task When_Cache_Is_Disposed_While_Waiting_For_Refresh_Refresh_Stops()
        {
            var options = this.quickRefreshOptions with
            {
                RefreshInterval = TimeSpan.FromSeconds(250)
            };
            var cache = new TestDeviceCache(options);
            await cache.DisposeAsync();

            var count = cache.RefreshOperationsCount;
            await Task.Delay(700);
            Assert.Equal(count, cache.RefreshOperationsCount);
        }

        [Fact]
        public async Task When_Device_Is_Fresh_No_Refresh_Is_Triggered()
        {
            await using var cache = new TestDeviceCache(this.quickRefreshOptions);
            await using var device = CreateTestDevice();
            device.LastUpdate = DateTime.UtcNow + TimeSpan.FromMinutes(1);

            cache.Register(device);
            using var cts = this.quickRefreshOptions.ValidationIntervalCancellationToken();
            await Assert.ThrowsAsync<OperationCanceledException>(() => cache.WaitForRefreshAsync(cts.Token));
        }

        [Fact]
        public async Task When_Disposed_While_Refreshing_We_Shutdown_Gracefully()
        {
            await using var cache = new TestDeviceCache(this.quickRefreshOptions, true);
            var deviceMock = CreateMockDevice();
            deviceMock.Setup(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NetworkServerConfiguration config, CancellationToken token) =>
                {
                    token.WaitHandle.WaitOne();
                    throw new LoRaProcessingException("Refresh failed.", LoRaProcessingErrorCode.DeviceInitializationFailed);
                });

            var device = deviceMock.Object;
            cache.Register(device);

            while (!deviceMock.Invocations.Any(x => x.Method.Name == nameof(LoRaDevice.InitializeAsync)))
            {
                await Task.Delay(5);
            }

            await cache.DisposeAsync();
            var count = cache.DeviceRefreshCount;
            await Task.Delay(this.quickRefreshOptions.ValidationIntervalDelay());
            Assert.Equal(count, cache.DeviceRefreshCount);
        }

        [Fact]
        public async Task When_Refresh_Fails_It_Is_Retried()
        {
            await using var cache = new TestDeviceCache(this.quickRefreshOptions, true);
            var deviceMock = CreateMockDevice();
            deviceMock.SetupSequence(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new LoRaProcessingException("Refresh failed.", LoRaProcessingErrorCode.DeviceInitializationFailed))
                .ReturnsAsync(true);

            var device = deviceMock.Object;
            cache.Register(device);

            await cache.WaitForRefreshCallsAsync(3);

            deviceMock.Verify(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
        }

        [Fact]
        public async Task When_Device_Inactive_It_Is_Removed()
        {
            var options = this.quickRefreshOptions with
            {
                MaxUnobservedLifetime = TimeSpan.FromMilliseconds(1)
            };
            await using var cache = new TestDeviceCache(options);

            var connectionManager = new Mock<ILoRaDeviceClientConnectionManager>();

            await using var device = new LoRaDevice(new DevAddr(0xabc), new DevEui(0x123), connectionManager.Object) { LastSeen = DateTime.UtcNow };

            cache.Register(device);
            using var cts = this.quickRefreshOptions.ValidationIntervalCancellationToken();
            await cache.WaitForRemoveAsync(cts.Token);

            Assert.False(cache.TryGetByDevEui(device.DevEUI, out _));
            connectionManager.Verify(x => x.ReleaseAsync(device), Times.Once);
        }

        [Fact]
        public async Task Trying_To_Remove_Device_That_Was_Not_Registered_Fails()
        {
            await using var cache = CreateNoRefreshCache();
            await using var device = CreateTestDevice();
            Assert.False(await cache.RemoveAsync(device));
        }

        [Fact]
        public async Task Remove_Registered_Device_Succeeds()
        {
            await using var cache = CreateNoRefreshCache();
            await using var device = CreateTestDevice();
            cache.Register(device);
            Assert.True(await cache.RemoveAsync(device));
            Assert.False(cache.TryGetByDevEui(device.DevEUI, out _));
        }

        [Fact]
        public async Task When_Last_Device_Is_Removed_DevAddr_Registry_Is_Cleared()
        {
            await using var cache = CreateNoRefreshCache();
            await using var device = CreateTestDevice();
            cache.Register(device);
            Assert.True(await cache.RemoveAsync(device));
            Assert.False(cache.HasRegistrations(device.DevAddr.Value));
        }

        [Fact]
        public async Task Adding_And_Removing_Join_Device_Succeeds()
        {
            await using var cache = CreateNoRefreshCache();
            await using var device = CreateTestDevice();
            device.DevAddr = null;
            cache.Register(device);

            Assert.True(cache.TryGetByDevEui(device.DevEUI, out _));
            ValidateStats(1, 0);
            Assert.True(await cache.RemoveAsync(device));
            Assert.False(cache.TryGetByDevEui(device.DevEUI, out _));
            ValidateStats(1, 1);

            void ValidateStats(int hit, int miss)
            {
                var cacheStats = cache.CalculateStatistics();
                Assert.Equal(hit, cacheStats.Hit);
                Assert.Equal(miss, cacheStats.Miss);
            }
        }

        [Fact]
        public async Task When_Removing_Device_Is_Disposed()
        {
            await using var cache = CreateNoRefreshCache();
            var deviceMock = CreateMockDevice();
            var device = deviceMock.Object;
            cache.Register(device);
            Assert.True(await cache.RemoveAsync(device));
            Assert.False(cache.TryGetByDevEui(device.DevEUI, out _));

            deviceMock.Protected().Verify("DisposeAsyncCore", Times.Once());
        }

        [Fact]
        public async Task Registering_And_Unregistering_Multiple_Devices_With_Matching_DevAddr_Succeeds()
        {
            await using var cache = CreateNoRefreshCache();
            await using var device1 = CreateTestDevice();
            await using var device2 = CreateTestDevice();
            device2.DevEUI = new DevEui(0xaaa);

            Assert.Equal(device1.DevAddr, device2.DevAddr);

            var devAddr = device1.DevAddr.Value;

            cache.Register(device1);
            cache.Register(device2);

            Assert.True(cache.HasRegistrations(devAddr));
            Assert.Equal(2, cache.RegistrationCount(devAddr));

            Assert.True(await cache.RemoveAsync(device1));
            Assert.True(cache.HasRegistrations(devAddr));
            Assert.Equal(1, cache.RegistrationCount(devAddr));

            Assert.True(await cache.RemoveAsync(device2));
            Assert.False(cache.HasRegistrations(devAddr));
            Assert.Equal(0, cache.RegistrationCount(devAddr));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Mic_Validation_Is_Used_To_Validate_Items(bool isValid)
        {
            await using var device = CreateTestDevice();
            await using var cache = new TestDeviceCache(this.noRefreshOptions, (_, _) => isValid);

            cache.Register(device);

            var payload = new LoRaPayloadData(device.DevAddr.Value, new MacHeader(MacMessageType.UnconfirmedDataUp),
                                              FrameControlFlags.None, 1, string.Empty, "payload", FramePort.AppMin,
                                              mic: null, NullLogger.Instance);

            Assert.Equal(isValid, cache.TryGetForPayload(payload, out _));

            var stats = cache.CalculateStatistics();

            Assert.Equal(isValid ? 1 : 0, stats.Hit);
            Assert.Equal(isValid ? 0 : 1, stats.Miss);
        }

        [Fact]
        public async Task Device_LastSeen_Is_Updated_When_Registered()
        {
            await using var device = CreateTestDevice();
            await using var cache = CreateNoRefreshCache();
            var initialLastSeen = device.LastSeen = DateTime.UtcNow - TimeSpan.FromMinutes(1);

            cache.Register(device);
            Assert.True(initialLastSeen < device.LastSeen);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Last_Seen_Is_Updated_Depending_On_Cache_Hit(bool cacheHit)
        {
            await using var device = CreateTestDevice();
            await using var cache = new TestDeviceCache(this.noRefreshOptions, (_, _) => cacheHit);

            cache.Register(device);

            var payload = new LoRaPayloadData(device.DevAddr.Value, new MacHeader(MacMessageType.UnconfirmedDataUp),
                                              FrameControlFlags.None, 1, string.Empty, "payload", FramePort.AppMin,
                                              mic: null, NullLogger.Instance);

            var lastSeen = device.LastSeen;
            cache.TryGetForPayload(payload, out _);

            if (cacheHit)
                Assert.True(device.LastSeen > lastSeen);
            else
                Assert.Equal(device.LastSeen, lastSeen);
        }

        [Fact]
        public async Task When_Trying_To_Cleanup_Same_DevAddress_Fails()
        {
            await using var cache = CreateNoRefreshCache();
            await using var device = CreateTestDevice();

            Assert.Throws<InvalidOperationException>(() => cache.CleanupOldDevAddrForDevice(device, device.DevAddr.Value));
        }

        [Fact]
        public async Task When_Trying_To_Cleanup_Non_Existing_Old_Address_Fails()
        {
            await using var cache = CreateNoRefreshCache();
            await using var device = CreateTestDevice();

            Assert.Throws<InvalidOperationException>(() => cache.CleanupOldDevAddrForDevice(device, new DevAddr(0x00ffffff)));
        }

        [Fact]
        public async Task When_Trying_To_Cleanup_OldDevAddr_With_Different_Device_Instance_Fails()
        {
            await using var cache = CreateNoRefreshCache();
            await using var device1 = CreateTestDevice();
            await using var device2 = CreateTestDevice();

            device2.DevAddr = new DevAddr(0x00ffffff);
            cache.Register(device2);

            Assert.Throws<InvalidOperationException>(() => cache.CleanupOldDevAddrForDevice(device1, new DevAddr(0x00ffffff)));
        }

        [Fact]
        public async Task When_Cleaning_Up_Old_DevAddr_Entry_Is_Removed()
        {
            await using var cache = CreateNoRefreshCache();
            await using var device = CreateTestDevice();

            cache.Register(device);

            var oldDevAddr = device.DevAddr.Value;
            device.DevAddr = new DevAddr(0x00ffffff);

            cache.CleanupOldDevAddrForDevice(device, oldDevAddr);
            Assert.False(cache.HasRegistrations(oldDevAddr));
        }

        [Fact]
        public async Task When_Registering_After_Join_With_Different_Device_Throws()
        {
            await using var cache = CreateNoRefreshCache();
            await using var device = CreateTestDevice();

            device.DevAddr = null;
            cache.Register(device);

            await using var device2 = CreateTestDevice();
            Assert.Throws<InvalidOperationException>(() => cache.Register(device2));
        }

        [Fact]
        public async Task When_Resetting_Cache_All_Connections_Are_Released()
        {
            await using var cache = CreateNoRefreshCache();
            var items = Enumerable.Range(1, 2).Select(x =>
            {
                var connectionMgr = new Mock<ILoRaDeviceClientConnectionManager>();
                var device = new LoRaDevice(new DevAddr(0xfffffff0 + checked((uint)x)), new DevEui(checked((ulong)x)), connectionMgr.Object);
                cache.Register(device);
                return (device, connectionMgr);
            }).ToArray();

            await cache.ResetAsync();

            foreach (var (device, connectionMgr) in items)
            {
                connectionMgr.Verify(x => x.ReleaseAsync(device), Times.Once);
            }
        }
        private static Mock<LoRaDevice> CreateMockDevice()
            => new Mock<LoRaDevice>(new DevAddr(200), new DevEui(100), Mock.Of<ILoRaDeviceClientConnectionManager>());

        private static LoRaDevice CreateTestDevice()
            => new LoRaDevice(new DevAddr(0xffffffff), new DevEui(0), null) { NwkSKey = TestKeys.CreateNetworkSessionKey(0xAAAAAAAA) };

        private readonly LoRaDeviceCacheOptions quickRefreshOptions = new LoRaDeviceCacheOptions { MaxUnobservedLifetime = TimeSpan.FromMilliseconds(int.MaxValue), RefreshInterval = TimeSpan.FromMilliseconds(1), ValidationInterval = TimeSpan.FromMilliseconds(50) };

        private readonly LoRaDeviceCacheOptions noRefreshOptions = new LoRaDeviceCacheOptions { MaxUnobservedLifetime = TimeSpan.FromMilliseconds(int.MaxValue), RefreshInterval = TimeSpan.FromMilliseconds(int.MaxValue), ValidationInterval = TimeSpan.FromMilliseconds(int.MaxValue) };

        private LoRaDeviceCache CreateNoRefreshCache() => new LoRaDeviceCache(this.noRefreshOptions, null, NullLogger<LoRaDeviceCache>.Instance, TestMeter.Instance);

        private class TestDeviceCache : LoRaDeviceCache
        {
            private readonly SemaphoreSlim refreshTick = new SemaphoreSlim(0);
            private readonly SemaphoreSlim removeTick = new SemaphoreSlim(0);
            private readonly Action<LoRaDevice> onRefreshDevice;
            private readonly Func<LoRaDevice, LoRaPayload, bool> validateMic;
            private readonly bool callDeviceRefresh;
            private readonly NetworkServerConfiguration configuration;
            private readonly LoRaDeviceCacheOptions cacheOptions;

            public int RefreshOperationsCount { get; private set; }
            public int DeviceRefreshCount { get; private set; }

            private TestDeviceCache(Action<LoRaDevice> onRefreshDevice, LoRaDeviceCacheOptions options, NetworkServerConfiguration networkServerConfiguration, bool callDeviceRefresh = false, Func<LoRaDevice, LoRaPayload, bool> validateMic = null) : base(options, networkServerConfiguration, NullLogger<LoRaDeviceCache>.Instance, TestMeter.Instance)
            {
                this.onRefreshDevice = onRefreshDevice;
                this.callDeviceRefresh = callDeviceRefresh;
                this.configuration = networkServerConfiguration;
                this.validateMic = validateMic;
                this.cacheOptions = options;
            }
            public TestDeviceCache(LoRaDeviceCacheOptions options, Func<LoRaDevice, LoRaPayload, bool> validateMic)
                : this(null, options, new NetworkServerConfiguration(), validateMic: validateMic)
            { }

            public TestDeviceCache(LoRaDeviceCacheOptions options, bool callDeviceRefresh)
                : this(null, options, new NetworkServerConfiguration(), callDeviceRefresh)
            { }
            public TestDeviceCache(LoRaDeviceCacheOptions options)
                : this(null, options, new NetworkServerConfiguration())
            { }

            public TestDeviceCache(Action<LoRaDevice> onRefreshDevice, LoRaDeviceCacheOptions options)
                : this(onRefreshDevice, options, new NetworkServerConfiguration())
            { }

            public TestDeviceCache(Action<LoRaDevice> onRefreshDevice, LoRaDeviceCacheOptions options, bool callDeviceRefresh)
                : this(onRefreshDevice, options, new NetworkServerConfiguration(), callDeviceRefresh: callDeviceRefresh)
            { }

            public TestDeviceCache(Action<LoRaDevice> onRefreshDevice)
                : this (onRefreshDevice, new LoRaDeviceCacheOptions { MaxUnobservedLifetime = TimeSpan.FromMilliseconds(int.MaxValue), RefreshInterval = TimeSpan.FromMilliseconds(int.MaxValue), ValidationInterval = TimeSpan.FromMilliseconds(int.MaxValue) }, new NetworkServerConfiguration())
            { }

            internal async Task WaitForRefreshAsync(CancellationToken cancellationToken) =>
                await this.refreshTick.WaitAsync(cancellationToken);

            internal async Task WaitForRefreshCallsAsync(int numberOfCalls, TimeSpan timeout = default)
            {
                if (timeout == TimeSpan.Zero)
                {
                    timeout = (this.cacheOptions.RefreshInterval * numberOfCalls) + TimeSpan.FromSeconds(5);
                }

                using var cts = new CancellationTokenSource(timeout);
                for (var i = 0; i < numberOfCalls; i++)
                {
                    await this.refreshTick.WaitAsync(cts.Token);
                }
            }

            internal async Task WaitForRemoveAsync(CancellationToken cancellationToken) =>
                await this.removeTick.WaitAsync(cancellationToken);

            protected override async Task RefreshDeviceAsync(LoRaDevice device, CancellationToken cancellationToken)
            {
                try
                {
                    this.onRefreshDevice?.Invoke(device);

                    if (this.callDeviceRefresh)
                        await base.RefreshDeviceAsync(device, cancellationToken);
                }
                finally
                {
                    if (this.refreshTick.CurrentCount == 0)
                        this.refreshTick.Release();

                    DeviceRefreshCount++;
                }
            }

            protected override void OnRefresh()
            {
                RefreshOperationsCount++;
            }

            public override async Task<bool> RemoveAsync(LoRaDevice device)
            {
                var ret = await base.RemoveAsync(device);
                if (this.removeTick.CurrentCount == 0)
                    this.removeTick.Release();
                return ret;
            }

            protected override bool ValidateMic(LoRaDevice loRaDevice, LoRaPayload loRaPayload)
            {
                return this.validateMic == null || this.validateMic(loRaDevice, loRaPayload);
            }

            protected override ValueTask DisposeAsync(bool dispose)
            {
                if (dispose)
                {
                    this.refreshTick.Dispose();
                    this.removeTick.Dispose();
                }
                return base.DisposeAsync(dispose);
            }
        }
    }
    internal static class LoRaDeviceCacheOptionsExtensions
    {
        public static TimeSpan ValidationIntervalDelay(this LoRaDeviceCacheOptions options)
         => options.ValidationInterval * 3;

        public static CancellationTokenSource ValidationIntervalCancellationToken(this LoRaDeviceCacheOptions options) => new CancellationTokenSource(options.ValidationIntervalDelay());
    }
}
