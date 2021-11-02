// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests.BasicsStation
{
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.Tests.Common;
    using LoRaWan.Tests.Unit.NetworkServerTests.JsonHandlers;
    using Microsoft.Azure.Devices.Client.Exceptions;
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
                await Assert.ThrowsAsync<InvalidOperationException>(() => this.sut.GetRouterConfigMessageAsync(this.stationEui, CancellationToken.None));
            }

            /// <summary>
            /// This test should become useful with https://github.com/Azure/iotedge-lorawan-starterkit/issues/565.
            /// </summary>
            [Theory]
            [InlineData(typeof(IotHubException))]
            [InlineData(typeof(IotHubCommunicationException))]
            public async Task Rethrows_Specific_Error_Cases(Type type)
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(this.stationEui, "foo");

                var ex = (Exception)Activator.CreateInstance(type);
                this.loRaDeviceFactoryMock.Setup(ldf => ldf.CreateDeviceClient(stationEui.ToString(), primaryKey))
                                          .Throws(ex);

                // act + assert
                await Assert.ThrowsAsync(type, () => this.sut.GetRouterConfigMessageAsync(this.stationEui, CancellationToken.None));
            }

            /// <summary>
            /// This test should become useful with https://github.com/Azure/iotedge-lorawan-starterkit/issues/565.
            /// </summary>
            [Fact]
            public async Task Rethrows_When_Router_Config_Not_Present()
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(this.stationEui, "foo");
                SetupTwinResponse(this.stationEui, primaryKey, @$"{{ ""foo"": ""bar"" }}");

                // act + assert
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => this.sut.GetRouterConfigMessageAsync(this.stationEui, CancellationToken.None));
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
                this.loRaDeviceApiServiceMock.Verify(ldf => ldf.SearchByDevEUIAsync(It.IsAny<string>()), Times.Once);
                foreach (var r in result)
                    Assert.Equal(JsonUtil.Minify(LnsStationConfigurationTests.ValidRouterConfigMessage), r);
            }

            [Fact]
            public async Task Resumes_After_Failure()
            {
                // arrange
                const string primaryKey = "foo";
                SetupTwinResponse(this.stationEui, primaryKey);
                this.loRaDeviceApiServiceMock.SetupSequence(ldas => ldas.SearchByDevEUIAsync(It.IsAny<string>()))
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
                this.loRaDeviceApiServiceMock.Verify(ldf => ldf.SearchByDevEUIAsync(It.IsAny<string>()), Times.Exactly(2));
                Assert.Equal(JsonUtil.Minify(LnsStationConfigurationTests.ValidRouterConfigMessage), result);
            }

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

            private void SetupDeviceKeyLookup(StationEui stationEui, string primaryKey) =>
                SetupDeviceKeyLookup(stationEui, new[] { new IoTHubDeviceInfo { DevEUI = this.stationEui.ToString(), PrimaryKey = primaryKey } });

            private void SetupDeviceKeyLookup(StationEui stationEui, params IoTHubDeviceInfo[] ioTHubDeviceInfos) =>
                loRaDeviceApiServiceMock.Setup(ldas => ldas.SearchByDevEUIAsync(stationEui.ToString()))
                                        .Returns(Task.FromResult(new SearchDevicesResult(ioTHubDeviceInfos)));
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
