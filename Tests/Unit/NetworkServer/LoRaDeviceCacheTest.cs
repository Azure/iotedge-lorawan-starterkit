// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using Moq;
    using Xunit;

    public class LoRaDeviceCacheTest
    {

        [Fact]
        public async Task When_Device_Expires_It_Is_Refreshed()
        {
            var moqCallback = new Mock<Action<LoRaDevice>>();
            using var cache = new TestDeviceCache(moqCallback.Object, this.quickRefreshOptions);
            using var device = new LoRaDevice("abc", "123", null);
            cache.Register(device);
            await cache.WaitForRefreshAsync(CancellationToken.None);
            moqCallback.Verify(x => x.Invoke(device));
        }

        [Fact]
        public async Task When_Device_Is_Fresh_No_Refresh_Is_Triggered()
        {
            using var cache = new TestDeviceCache(this.quickRefreshOptions);
            using var device = new LoRaDevice("abc", "123", null) { LastUpdate = DateTime.UtcNow + TimeSpan.FromMinutes(1) };
            cache.Register(device);
            using var cts = new CancellationTokenSource(this.quickRefreshOptions.ValidationInterval * 2);
            await Assert.ThrowsAsync<OperationCanceledException>(() => cache.WaitForRefreshAsync(cts.Token));
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

        private readonly LoRaDeviceCacheOptions quickRefreshOptions = new LoRaDeviceCacheOptions { MaxUnobservedLifetime = TimeSpan.MaxValue, RefreshInterval = TimeSpan.FromMilliseconds(1), ValidationInterval = TimeSpan.FromMilliseconds(50) };

        private class TestDeviceCache : LoRaDeviceCache
        {
            private readonly SemaphoreSlim refreshTick = new SemaphoreSlim(0);
            private readonly SemaphoreSlim removeTick = new SemaphoreSlim(0);
            private readonly Action<LoRaDevice> onRefreshDevice;

            public int RefreshCount { get; private set; }

            private TestDeviceCache(Action<LoRaDevice> onRefreshDevice, LoRaDeviceCacheOptions options, NetworkServerConfiguration networkServerConfiguration) : base(options, networkServerConfiguration)
            {
                this.onRefreshDevice = onRefreshDevice;
            }

            public TestDeviceCache(LoRaDeviceCacheOptions options)
                : this(null, options, new NetworkServerConfiguration())
            {
            }

            public TestDeviceCache(Action<LoRaDevice> onRefreshDevice, LoRaDeviceCacheOptions options)
                : this(onRefreshDevice, options, new NetworkServerConfiguration())
            {
            }

            public TestDeviceCache(Action<LoRaDevice> onRefreshDevice)
                : this (onRefreshDevice, new LoRaDeviceCacheOptions { MaxUnobservedLifetime = TimeSpan.MaxValue, RefreshInterval = TimeSpan.MaxValue, ValidationInterval = TimeSpan.MaxValue }, new NetworkServerConfiguration())
            {
            }

            internal async Task WaitForRefreshAsync(CancellationToken cancellationToken) => 
                await this.refreshTick.WaitAsync(cancellationToken);

            internal async Task WaitForRemoveAsync(CancellationToken cancellationToken) =>
                await this.removeTick.WaitAsync(cancellationToken);


            protected override Task RefreshDeviceAsync(LoRaDevice device, CancellationToken cancellationToken)
            {
                this.onRefreshDevice?.Invoke(device);
                if(this.refreshTick.CurrentCount == 0)
                    this.refreshTick.Release();

                RefreshCount++;

                return Task.CompletedTask;
            }

            public override bool Remove(LoRaDevice loRaDevice)
            {
                if (this.removeTick.CurrentCount == 0)
                    this.removeTick.Release();

                var ret = base.Remove(loRaDevice);

                return ret;
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
