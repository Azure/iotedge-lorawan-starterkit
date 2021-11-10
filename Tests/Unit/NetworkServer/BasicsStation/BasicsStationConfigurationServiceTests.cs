// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer.BasicsStation
{
    using global::LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.Tests.Common;
    using LoRaWan.Tests.Unit.NetworkServer.BasicsStation.JsonHandlers;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class BasicsStationConfigurationServiceTests : IDisposable
    {
        private readonly Mock<LoRaDeviceAPIServiceBase> loRaDeviceApiServiceMock;
        private readonly Mock<ILoRaDeviceFactory> loRaDeviceFactoryMock;
        private readonly IMemoryCache memoryCache;
        private readonly StationEui stationEui;
        private readonly BasicsStationConfigurationService sut;
        private bool disposedValue;

        public BasicsStationConfigurationServiceTests()
        {
            this.stationEui = new StationEui(ulong.MaxValue);
            this.loRaDeviceApiServiceMock = new Mock<LoRaDeviceAPIServiceBase>();
            this.loRaDeviceFactoryMock = new Mock<ILoRaDeviceFactory>();
            this.memoryCache = new MemoryCache(new MemoryCacheOptions());
            this.sut = new BasicsStationConfigurationService(this.loRaDeviceApiServiceMock.Object,
                                                             this.loRaDeviceFactoryMock.Object,
                                                             this.memoryCache);
        }


        private void SetupDeviceKeyLookup(StationEui stationEui, string primaryKey) =>
            SetupDeviceKeyLookup(stationEui, new[] { new IoTHubDeviceInfo { DevEUI = this.stationEui.ToString(), PrimaryKey = primaryKey } });

        private void SetupDeviceKeyLookup(StationEui stationEui, params IoTHubDeviceInfo[] ioTHubDeviceInfos) =>
            loRaDeviceApiServiceMock.Setup(ldas => ldas.SearchByEuiAsync(stationEui))
                                    .Returns(Task.FromResult(new SearchDevicesResult(ioTHubDeviceInfos)));

        private void SetupTwinResponse(StationEui stationEui, string primaryKey) =>
            SetupTwinResponse(stationEui, primaryKey, @$"{{ ""routerConfig"": {JsonUtil.Minify(LnsStationConfigurationTests.ValidStationConfiguration)} }}");

        private void SetupTwinResponse(StationEui stationEui, string primaryKey, string json)
        {
            var deviceTwin = new Twin(new TwinProperties { Desired = new TwinCollection(json) });
            var deviceClientMock = new Mock<ILoRaDeviceClient>();
            deviceClientMock.Setup(dc => dc.GetTwinAsync()).Returns(Task.FromResult(deviceTwin));
            this.loRaDeviceFactoryMock.Setup(ldf => ldf.CreateDeviceClient(stationEui.ToString(), primaryKey))
                                      .Returns(deviceClientMock.Object);
        }

        public class GetRegionAsync : BasicsStationConfigurationServiceTests
        {
            [Fact]
            public async Task Success()
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(this.stationEui, primaryKey);
                SetupTwinResponse(this.stationEui, primaryKey);

                // act
                var result = await this.sut.GetRegionAsync(this.stationEui, CancellationToken.None);

                // assert
                Assert.Equal(RegionManager.EU868, result);
            }
        }

        public class GetRouterConfigMessageAsync : BasicsStationConfigurationServiceTests
        {
            [Fact]
            public async Task Success()
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(this.stationEui, primaryKey);
                SetupTwinResponse(this.stationEui, primaryKey);

                // act
                var result = await this.sut.GetRouterConfigMessageAsync(this.stationEui, CancellationToken.None);

                // assert
                Assert.Equal(JsonUtil.Minify(LnsStationConfigurationTests.ValidRouterConfigMessage), result);
            }

            [Theory]
            [InlineData(0)]
            [InlineData(2)]
            public async Task When_Device_Key_Lookup_Is_Non_Unique_Fails(int count)
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(this.stationEui, Enumerable.Range(0, count).Select(_ => new IoTHubDeviceInfo()).ToArray());
                SetupTwinResponse(this.stationEui, primaryKey);

                // act + assert
                var ex = await Assert.ThrowsAsync<LoRaProcessingException>(() => this.sut.GetRouterConfigMessageAsync(this.stationEui, CancellationToken.None));
                Assert.Equal(LoRaProcessingErrorCode.InvalidDeviceConfiguration, ex.ErrorCode);
            }

            [Fact]
            public async Task Caches_And_Handles_Concurrent_Access()
            {
                // arrange
                const int numberOfConcurrentAccess = 5;
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(this.stationEui, primaryKey);
                SetupTwinResponse(this.stationEui, primaryKey);

                // act
                var result = await Task.WhenAll(from i in Enumerable.Range(0, numberOfConcurrentAccess)
                                                select this.sut.GetRouterConfigMessageAsync(this.stationEui, CancellationToken.None));

                // assert
                Assert.Equal(result.Length, numberOfConcurrentAccess);
                this.loRaDeviceFactoryMock.Verify(ldf => ldf.CreateDeviceClient(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
                this.loRaDeviceApiServiceMock.Verify(ldf => ldf.SearchByEuiAsync(It.IsAny<StationEui>()), Times.Once);
                foreach (var r in result)
                    Assert.Equal(JsonUtil.Minify(LnsStationConfigurationTests.ValidRouterConfigMessage), r);
            }

            [Fact]
            public async Task Rethrows_When_Router_Config_Not_Present()
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(this.stationEui, "foo");
                SetupTwinResponse(this.stationEui, primaryKey, @$"{{ ""foo"": ""bar"" }}");

                // act + assert
                var ex = await Assert.ThrowsAsync<LoRaProcessingException>(() => this.sut.GetRouterConfigMessageAsync(this.stationEui, CancellationToken.None));
                Assert.Equal(LoRaProcessingErrorCode.InvalidDeviceConfiguration, ex.ErrorCode);
            }

            [Fact]
            public async Task Resumes_After_Failure()
            {
                // arrange
                const string primaryKey = "foo";
                SetupTwinResponse(this.stationEui, primaryKey);
                this.loRaDeviceApiServiceMock.SetupSequence(ldas => ldas.SearchByEuiAsync(It.IsAny<StationEui>()))
                                             .Throws(new InvalidOperationException())
                                             .Returns(Task.FromResult(new SearchDevicesResult(new[]
                                             {
                                                 new IoTHubDeviceInfo { DevEUI = this.stationEui.ToString(), PrimaryKey = primaryKey }
                                             })));

                // act
                Task<string> Act() => this.sut.GetRouterConfigMessageAsync(this.stationEui, CancellationToken.None);
                await Assert.ThrowsAsync<InvalidOperationException>(Act);
                var result = await Act();

                // assert
                this.loRaDeviceApiServiceMock.Verify(ldf => ldf.SearchByEuiAsync(It.IsAny<StationEui>()), Times.Exactly(2));
                Assert.Equal(JsonUtil.Minify(LnsStationConfigurationTests.ValidRouterConfigMessage), result);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.sut.Dispose();
                    this.memoryCache.Dispose();
                }

                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
