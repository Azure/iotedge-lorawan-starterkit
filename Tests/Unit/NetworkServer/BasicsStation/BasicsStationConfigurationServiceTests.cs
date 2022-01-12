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
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using static Bandwidth;
    using static SpreadingFactor;

    public class BasicsStationConfigurationServiceTests : IDisposable
    {
        private readonly Mock<LoRaDeviceAPIServiceBase> loRaDeviceApiServiceMock;
        private readonly Mock<ILoRaDeviceFactory> loRaDeviceFactoryMock;
        private readonly IMemoryCache memoryCache;
        private readonly StationEui stationEui;
        private readonly DevEui devEui;
        private readonly BasicsStationConfigurationService sut;
        private bool disposedValue;

        public BasicsStationConfigurationServiceTests()
        {
            this.stationEui = new StationEui(ulong.MaxValue);
            this.devEui = DevEui.Parse(this.stationEui.ToString());
            this.loRaDeviceApiServiceMock = new Mock<LoRaDeviceAPIServiceBase>();
            this.loRaDeviceFactoryMock = new Mock<ILoRaDeviceFactory>();
            this.memoryCache = new MemoryCache(new MemoryCacheOptions());
            this.sut = new BasicsStationConfigurationService(this.loRaDeviceApiServiceMock.Object,
                                                             this.loRaDeviceFactoryMock.Object,
                                                             this.memoryCache,
                                                             NullLogger<BasicsStationConfigurationService>.Instance);
        }

        private void SetupDeviceKeyLookup() => SetupDeviceKeyLookup("foo");

        private void SetupDeviceKeyLookup(string primaryKey) =>
            SetupDeviceKeyLookup(new[] { new IoTHubDeviceInfo { DevEUI = this.devEui, PrimaryKey = primaryKey } });

        private void SetupDeviceKeyLookup(params IoTHubDeviceInfo[] ioTHubDeviceInfos) =>
            loRaDeviceApiServiceMock.Setup(ldas => ldas.SearchByEuiAsync(this.stationEui))
                                    .Returns(Task.FromResult(new SearchDevicesResult(ioTHubDeviceInfos)));

        private const string TcUri = "wss://tc.local:5001";
        private const string CupsUri = "https://cups.local:5002";

        private static string GetTxParamsJson(DwellTimeSetting setting)
        {
            if (setting is null)
                return "null";

            static string Serialize(object obj) => JsonSerializer.Serialize(obj);

            const string template = @"{{
                ""eirp"": {0},
                ""uplinkDwellLimit"": {1},
                ""downlinkDwellLimit"": {2}
            }}";

            return JsonUtil.Minify(string.Format(CultureInfo.InvariantCulture, template,
                                                 Serialize(setting.MaxEirp),
                                                 Serialize(setting.UplinkDwellTime),
                                                 Serialize(setting.DownlinkDwellTime)));
        }

        private void SetupTwinResponse(LoRaRegionType region, DwellTimeSetting desiredDwellTimeSetting) =>
            SetupTwinResponse("foo", region, desiredDwellTimeSetting);

        private void SetupTwinResponse(string primaryKey) =>
            SetupTwinResponse(primaryKey, LoRaRegionType.EU863, new DwellTimeSetting(false, false, 0));


        private void SetupTwinResponse(string primaryKey, LoRaRegionType region, DwellTimeSetting desiredDwellTimeSetting) =>
            SetupTwinResponse(primaryKey, @$"{{ ""routerConfig"": {GetRouterConfigurationJson(region)},
                                                ""clientThumbprint"": [ ""thumbprint"" ],
                                                ""cups"": {{
                                                            ""tcCredentialUrl"": ""https://storageurl.net/container/blob"",
                                                            ""tcCredCrc"":101938194,
                                                            ""cupsCredentialUrl"": ""https://storageurl.net/container/blob"",
                                                            ""cupsCredCrc"":101938194,
                                                            ""cupsUri"": ""{CupsUri}"",
                                                            ""tcUri"": ""{TcUri}""
                                                }},
                                                ""desiredTxParams"": {GetTxParamsJson(desiredDwellTimeSetting)}
                                              }}");

        private static string GetRouterConfigurationJson(LoRaRegionType region) =>
            JsonUtil.Minify(LnsStationConfigurationTests.GetTwinConfigurationJson(new[] { new NetId(1) },
                                                                                  new[] { (new JoinEui(ulong.MinValue), new JoinEui(ulong.MaxValue)) },
                                                                                  region.ToString(),
                                                                                  "sx1301/1",
                                                                                  (new Hertz(863000000), new Hertz(870000000)),
                                                                                  new[]
                                                                                  {
                                                                                      (SF11, BW125, false),
                                                                                      (SF10, BW125, false),
                                                                                      (SF9 , BW125, false),
                                                                                      (SF8 , BW125, false),
                                                                                      (SF7 , BW125, false),
                                                                                      (SF7 , BW250, false),
                                                                                  },
                                                                                  flags: RouterConfigStationFlags.NoClearChannelAssessment | RouterConfigStationFlags.NoDutyCycle | RouterConfigStationFlags.NoDwellTimeLimitations));

        private void SetupTwinResponse(string primaryKey, string json)
        {
            var deviceTwin = new Twin(new TwinProperties { Desired = new TwinCollection(json) });
            var deviceClientMock = new Mock<ILoRaDeviceClient>();
            deviceClientMock.Setup(dc => dc.GetTwinAsync(CancellationToken.None)).Returns(Task.FromResult(deviceTwin));
            this.loRaDeviceFactoryMock.Setup(ldf => ldf.CreateDeviceClient(this.devEui, primaryKey))
                                      .Returns(deviceClientMock.Object);
        }

        public class GetRegionAsync : BasicsStationConfigurationServiceTests
        {
            [Fact]
            public async Task Success()
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(primaryKey);
                SetupTwinResponse(primaryKey);

                // act
                var result = await this.sut.GetRegionAsync(this.stationEui, CancellationToken.None);

                // assert
                Assert.Equal(RegionManager.EU868, result);
            }

            [Fact]
            public async Task DwellTimeRegion_Success()
            {
                // arrange
                var expectedDesired = new DwellTimeSetting(true, false, 2);
                SetupDeviceKeyLookup();
                SetupTwinResponse(LoRaRegionType.AS923, expectedDesired);

                // act
                var result = await this.sut.GetRegionAsync(this.stationEui, CancellationToken.None);

                // assert
                var region = Assert.IsType<RegionAS923>(result);
                Assert.Equal(expectedDesired, region.DesiredDwellTimeSetting);
            }
        }
        public class GetAllowedClientThumbprintsAsync : BasicsStationConfigurationServiceTests
        {
            [Fact]
            public async Task Success()
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(primaryKey);
                SetupTwinResponse(primaryKey);

                // act
                var result = await this.sut.GetAllowedClientThumbprintsAsync(this.stationEui, CancellationToken.None);

                // assert
                Assert.Contains("thumbprint", result);
            }

            [Fact]
            public async Task Fails_WithoutProperty()
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(primaryKey);
                SetupTwinResponse(primaryKey, JsonUtil.Strictify("{ 'anotherProp': '1'}"));

                // act and assert
                var exception = await Assert.ThrowsAsync<LoRaProcessingException>(() => this.sut.GetAllowedClientThumbprintsAsync(this.stationEui, CancellationToken.None));
                Assert.Equal(LoRaProcessingErrorCode.InvalidDeviceConfiguration, exception.ErrorCode);
            }

            [Fact]
            public async Task Fails_With_InvalidCast()
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(primaryKey);
                SetupTwinResponse(primaryKey, JsonUtil.Strictify("{ 'clientThumbprint': 'x'}"));

                // act and assert
                var exception = await Assert.ThrowsAsync<LoRaProcessingException>(() => this.sut.GetAllowedClientThumbprintsAsync(this.stationEui, CancellationToken.None));
                Assert.Equal(LoRaProcessingErrorCode.InvalidDeviceConfiguration, exception.ErrorCode);
                Assert.IsType<InvalidCastException>(exception.InnerException);
            }
        }

        public class GetCupsConfigAsync : BasicsStationConfigurationServiceTests
        {
            [Fact]
            public async Task Success()
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(primaryKey);
                SetupTwinResponse(primaryKey);

                // act
                var result = await this.sut.GetCupsConfigAsync(this.stationEui, CancellationToken.None);

                // assert
                Assert.Equal(new Uri(TcUri), result.TcUri);
                Assert.Equal(new Uri(CupsUri), result.CupsUri);
                Assert.NotEqual(0U, result.TcCredCrc);
                Assert.NotEqual(0U, result.CupsCredCrc);
            }

            [Fact]
            public async Task Fails_WithoutProperty()
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(primaryKey);
                SetupTwinResponse(primaryKey, JsonUtil.Strictify("{ 'anotherProp': '1'}"));

                // act and assert
                var exception = await Assert.ThrowsAsync<LoRaProcessingException>(() => this.sut.GetCupsConfigAsync(this.stationEui, CancellationToken.None));
                Assert.Equal(LoRaProcessingErrorCode.InvalidDeviceConfiguration, exception.ErrorCode);
            }
        }

        public class GetRouterConfigMessageAsync : BasicsStationConfigurationServiceTests
        {
            [Fact]
            public async Task Success()
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(primaryKey);
                SetupTwinResponse(primaryKey);

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
                SetupDeviceKeyLookup(Enumerable.Range(0, count).Select(_ => new IoTHubDeviceInfo()).ToArray());
                SetupTwinResponse(primaryKey);

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
                SetupDeviceKeyLookup(primaryKey);
                SetupTwinResponse(primaryKey);

                // act
                var result = await Task.WhenAll(from i in Enumerable.Range(0, numberOfConcurrentAccess)
                                                select this.sut.GetRouterConfigMessageAsync(this.stationEui, CancellationToken.None));

                // assert
                Assert.Equal(result.Length, numberOfConcurrentAccess);
                this.loRaDeviceFactoryMock.Verify(ldf => ldf.CreateDeviceClient(It.IsAny<DevEui>(), It.IsAny<string>()), Times.Once);
                this.loRaDeviceApiServiceMock.Verify(ldf => ldf.SearchByEuiAsync(It.IsAny<StationEui>()), Times.Once);
                foreach (var r in result)
                    Assert.Equal(JsonUtil.Minify(LnsStationConfigurationTests.ValidRouterConfigMessage), r);
            }

            [Fact]
            public async Task Rethrows_When_Router_Config_Not_Present()
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup("foo");
                SetupTwinResponse(primaryKey, @$"{{ ""foo"": ""bar"" }}");

                // act + assert
                var ex = await Assert.ThrowsAsync<LoRaProcessingException>(() => this.sut.GetRouterConfigMessageAsync(this.stationEui, CancellationToken.None));
                Assert.Equal(LoRaProcessingErrorCode.InvalidDeviceConfiguration, ex.ErrorCode);
            }

            [Fact]
            public async Task Resumes_After_Failure()
            {
                // arrange
                const string primaryKey = "foo";
                SetupTwinResponse(primaryKey);
                this.loRaDeviceApiServiceMock.SetupSequence(ldas => ldas.SearchByEuiAsync(It.IsAny<StationEui>()))
                                             .Throws(new InvalidOperationException())
                                             .Returns(Task.FromResult(new SearchDevicesResult(new[]
                                             {
                                                 new IoTHubDeviceInfo { DevEUI = this.devEui, PrimaryKey = primaryKey }
                                             })));

                // act
                Task<string> Act() => this.sut.GetRouterConfigMessageAsync(this.stationEui, CancellationToken.None);
                await Assert.ThrowsAsync<InvalidOperationException>(Act);
                var result = await Act();

                // assert
                this.loRaDeviceApiServiceMock.Verify(ldf => ldf.SearchByEuiAsync(It.IsAny<StationEui>()), Times.Exactly(2));
                Assert.Equal(JsonUtil.Minify(LnsStationConfigurationTests.ValidRouterConfigMessage), result);
            }

            /// <summary>
            /// Since stations are also devices in IoT Hub, we use the <see cref="IoTHubDeviceInfo"/> when searching for station configuration.
            /// This requires that we can interpret a station EUI also as a device EUI. If that property does not hold, we need to change the way we query
            /// IoT Hub for station devices.
            /// </summary>
            [Fact]
            public void StationEui_Can_Be_Interpreted_As_Dev_Eui()
            {
                const ulong value = 0x1a2b3c;
                var devEui = new DevEui(value);
                var stationEui = new StationEui(value);
                Assert.Equal(devEui.ToString(), stationEui.ToString());
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
