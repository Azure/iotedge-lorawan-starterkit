// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools.LoRaMessage;
    using global::LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public class LoRaDeviceCacheTest
    {

        [Fact]
        public async Task When_Device_Expires_It_Is_Refreshed()
        {
            var moqCallback = new Mock<Action<LoRaDevice>>();
            using var cache = new TestDeviceCache(moqCallback.Object, this.quickRefreshOptions);
            using var device = CreateTestDevice();
            cache.Register(device);
            await cache.WaitForRefreshAsync(CancellationToken.None);
            moqCallback.Verify(x => x.Invoke(device));
        }

        [Fact]
        public async Task When_Cache_Is_Disposed_While_Waiting_For_Refresh_Refresh_Stops()
        {
            var options = this.quickRefreshOptions;
            options.RefreshInterval = TimeSpan.FromSeconds(250);
            var cache = new TestDeviceCache(this.quickRefreshOptions);
            cache.Dispose();

            var count = cache.RefreshOperationsCount;
            await Task.Delay(700);
            Assert.Equal(count, cache.RefreshOperationsCount);
        }

        [Fact]
        public async Task When_Device_Is_Fresh_No_Refresh_Is_Triggered()
        {
            using var cache = new TestDeviceCache(this.quickRefreshOptions);
            using var device = CreateTestDevice();
            device.LastUpdate = DateTime.UtcNow + TimeSpan.FromMinutes(1);

            cache.Register(device);
            using var cts = new CancellationTokenSource(this.quickRefreshOptions.ValidationInterval * 2);
            await Assert.ThrowsAsync<OperationCanceledException>(() => cache.WaitForRefreshAsync(cts.Token));
        }

        [Fact]
        public async Task When_Disposed_While_Refreshing_We_Shutdown_Gracefully()
        {
            using var cache = new TestDeviceCache(this.quickRefreshOptions, true);
            var deviceMock = new Mock<LoRaDevice>("abc", "123", null);
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

            cache.Dispose();
            var count = cache.DeviceRefreshCount;
            await Task.Delay(this.quickRefreshOptions.ValidationInterval * 2);
            Assert.Equal(count, cache.DeviceRefreshCount);
        }

        [Fact]
        public async Task When_Refresh_Fails_It_Is_Retried()
        {
            using var cache = new TestDeviceCache(this.quickRefreshOptions, true);
            var deviceMock = new Mock<LoRaDevice>("abc", "123", null);
            deviceMock.SetupSequence(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new LoRaProcessingException("Refresh failed.", LoRaProcessingErrorCode.DeviceInitializationFailed))
                .ReturnsAsync(true);

            var device = deviceMock.Object;
            cache.Register(device);

            await Task.Delay(this.quickRefreshOptions.ValidationInterval * 4);
            deviceMock.Verify(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
        }

        [Fact]
        public async Task When_Device_Inactive_It_Is_Removed()
        {
            var options = this.quickRefreshOptions;
            options.MaxUnobservedLifetime = TimeSpan.FromMilliseconds(1);

            using var cache = new TestDeviceCache(this.quickRefreshOptions);
            var connectionManager = new Mock<ILoRaDeviceClientConnectionManager>();

            using var device = new LoRaDevice("abc", "123", connectionManager.Object) { LastSeen = DateTime.UtcNow };

            cache.Register(device);
            using var cts = new CancellationTokenSource(this.quickRefreshOptions.ValidationInterval * 2);
            await cache.WaitForRemoveAsync(cts.Token);

            Assert.False(cache.TryGetByDevEui(device.DevEUI, out _));
            connectionManager.Verify(x => x.Release(device), Times.Once);
        }

        [Fact]
        public void Trying_To_Remove_Device_That_Was_Not_Registered_Fails()
        {
            using var cache = CreateNoRefreshCache();
            using var device = CreateTestDevice();
            Assert.False(cache.Remove(device));
        }

        [Fact]
        public void Remove_Registered_Device_Succeeds()
        {
            using var cache = CreateNoRefreshCache();
            using var device = CreateTestDevice();
            cache.Register(device);
            Assert.True(cache.Remove(device));
            Assert.False(cache.TryGetByDevEui(device.DevEUI, out _));
        }

        [Fact]
        public void When_Last_Device_Is_Removed_DevAddr_Registry_Is_Cleared()
        {
            using var cache = CreateNoRefreshCache();
            using var device = CreateTestDevice();
            cache.Register(device);
            Assert.True(cache.Remove(device));
            Assert.False(cache.HasRegistrations(device.DevAddr));
        }

        [Fact]
        public void Adding_And_Removing_Join_Device_Succeeds()
        {
            using var cache = CreateNoRefreshCache();
            using var device = CreateTestDevice();
            device.DevAddr = null;
            cache.Register(device);

            Assert.True(cache.TryGetByDevEui(device.DevEUI, out _));
            ValidateStats(1, 0);
            Assert.True(cache.Remove(device));
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
        public void Registering_And_Unregistering_Multiple_Devices_With_Matching_DevAddr_Succeeds()
        {
            using var cache = CreateNoRefreshCache();
            using var device1 = CreateTestDevice();
            using var device2 = CreateTestDevice();
            device2.DevEUI = "AAA";

            Assert.Equal(device1.DevAddr, device2.DevAddr);

            var devAddr = device1.DevAddr;

            cache.Register(device1);
            cache.Register(device2);

            Assert.True(cache.HasRegistrations(devAddr));
            Assert.Equal(2, cache.RegistrationCount(devAddr));

            Assert.True(cache.Remove(device1));
            Assert.True(cache.HasRegistrations(devAddr));
            Assert.Equal(1, cache.RegistrationCount(devAddr));

            Assert.True(cache.Remove(device2));
            Assert.False(cache.HasRegistrations(devAddr));
            Assert.Equal(0, cache.RegistrationCount(devAddr));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Mic_Validation_Is_Used_To_Validate_Items(bool isValid)
        {
            using var device = CreateTestDevice();
            using var cache = new TestDeviceCache(this.noRefreshOptions, (_, _) => isValid);

            cache.Register(device);

            var payload = new LoRaPayloadData
            {
                DevAddr = ConversionHelper.StringToByteArray(device.DevAddr)
            };

            Assert.Equal(isValid, cache.TryGetForPayload(payload, out _));

            var stats = cache.CalculateStatistics();

            Assert.Equal(isValid ? 1 : 0, stats.Hit);
            Assert.Equal(isValid ? 0 : 1, stats.Miss);
        }

        [Fact]
        public void Device_LastSeen_Is_Updated_When_Registered()
        {
            using var device = CreateTestDevice();
            using var cache = CreateNoRefreshCache();
            var initialLastSeen = device.LastSeen = DateTime.UtcNow - TimeSpan.FromMinutes(1);

            cache.Register(device);
            Assert.True(initialLastSeen < device.LastSeen);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Last_Seen_Is_Updated_Depending_On_Cache_Hit(bool cacheHit)
        {
            using var device = CreateTestDevice();
            using var cache = new TestDeviceCache(this.noRefreshOptions, (_, _) => cacheHit);

            cache.Register(device);

            var payload = new LoRaPayloadData
            {
                DevAddr = ConversionHelper.StringToByteArray(device.DevAddr)
            };

            var lastSeen = device.LastSeen;
            cache.TryGetForPayload(payload, out _);

            if (cacheHit)
                Assert.True(device.LastSeen > lastSeen);
            else
                Assert.Equal(device.LastSeen, lastSeen);
        }

        private static LoRaDevice CreateTestDevice() => new LoRaDevice("FFFFFFFF", "0000000000000000", null) { NwkSKey = "AAAAAAAA" };

        private readonly LoRaDeviceCacheOptions quickRefreshOptions = new LoRaDeviceCacheOptions { MaxUnobservedLifetime = TimeSpan.MaxValue, RefreshInterval = TimeSpan.FromMilliseconds(1), ValidationInterval = TimeSpan.FromMilliseconds(50) };

        private readonly LoRaDeviceCacheOptions noRefreshOptions = new LoRaDeviceCacheOptions { MaxUnobservedLifetime = TimeSpan.MaxValue, RefreshInterval = TimeSpan.MaxValue, ValidationInterval = TimeSpan.MaxValue };

        private LoRaDeviceCache CreateNoRefreshCache() => new LoRaDeviceCache(this.noRefreshOptions, null, NullLogger<LoRaDeviceCache>.Instance);

        private class TestDeviceCache : LoRaDeviceCache
        {
            private readonly SemaphoreSlim refreshTick = new SemaphoreSlim(0);
            private readonly SemaphoreSlim removeTick = new SemaphoreSlim(0);
            private readonly Action<LoRaDevice> onRefreshDevice;
            private readonly Func<LoRaDevice, LoRaPayload, bool> validateMic;
            private readonly bool callDeviceRefresh;
            private readonly NetworkServerConfiguration configuration;

            public int RefreshOperationsCount { get; private set; }
            public int DeviceRefreshCount { get; private set; }

            private TestDeviceCache(Action<LoRaDevice> onRefreshDevice, LoRaDeviceCacheOptions options, NetworkServerConfiguration networkServerConfiguration, bool callDeviceRefresh = false, Func<LoRaDevice, LoRaPayload, bool> validateMic = null) : base(options, networkServerConfiguration, NullLogger<LoRaDeviceCache>.Instance)
            {
                this.onRefreshDevice = onRefreshDevice;
                this.callDeviceRefresh = callDeviceRefresh;
                this.configuration = networkServerConfiguration;
                this.validateMic = validateMic;
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

            public TestDeviceCache(Action<LoRaDevice> onRefreshDevice)
                : this (onRefreshDevice, new LoRaDeviceCacheOptions { MaxUnobservedLifetime = TimeSpan.MaxValue, RefreshInterval = TimeSpan.MaxValue, ValidationInterval = TimeSpan.MaxValue }, new NetworkServerConfiguration())
            { }

            internal async Task WaitForRefreshAsync(CancellationToken cancellationToken) => 
                await this.refreshTick.WaitAsync(cancellationToken);

            internal async Task WaitForRemoveAsync(CancellationToken cancellationToken) =>
                await this.removeTick.WaitAsync(cancellationToken);

            protected override async Task RefreshDeviceAsync(LoRaDevice device, CancellationToken cancellationToken)
            {
                this.onRefreshDevice?.Invoke(device);
                if (this.refreshTick.CurrentCount == 0)
                    this.refreshTick.Release();

                if (this.callDeviceRefresh)
                {
                    await base.RefreshDeviceAsync(device, cancellationToken);
                }

                DeviceRefreshCount++;
            }

            protected override void OnRefresh()
            {
                RefreshOperationsCount++;
            }

            public override bool Remove(LoRaDevice loRaDevice)
            {
                if (this.removeTick.CurrentCount == 0)
                    this.removeTick.Release();

                var ret = base.Remove(loRaDevice);

                return ret;
            }

            protected override bool ValidateMic(LoRaDevice loRaDevice, LoRaPayload loRaPayload)
            {
                return this.validateMic == null || this.validateMic(loRaDevice, loRaPayload);
            }

            protected override void Dispose(bool dispose)
            {
                if (dispose)
                {
                    this.refreshTick.Dispose();
                    this.removeTick.Dispose();
                }
                base.Dispose(dispose);
            }
        }
    }
}
