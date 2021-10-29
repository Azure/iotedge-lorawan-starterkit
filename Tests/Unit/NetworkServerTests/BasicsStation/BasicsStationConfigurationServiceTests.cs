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
    using Moq;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class BasicsStationConfigurationServiceTests
    {
        private readonly Mock<LoRaDeviceAPIServiceBase> loRaDeviceApiServiceMock;
        private readonly Mock<ILoRaDeviceFactory> loRaDeviceFactoryMock;
        private readonly StationEui stationEui;
        private readonly BasicsStationConfigurationService sut;

        public BasicsStationConfigurationServiceTests()
        {
            this.stationEui = new StationEui(ulong.MaxValue);
            this.loRaDeviceApiServiceMock = new Mock<LoRaDeviceAPIServiceBase>();
            this.loRaDeviceFactoryMock = new Mock<ILoRaDeviceFactory>();
            this.sut = new BasicsStationConfigurationService(this.loRaDeviceApiServiceMock.Object, this.loRaDeviceFactoryMock.Object);
        }

        public class GetRouterConfigMessageAsync : BasicsStationConfigurationServiceTests
        {
            [Fact]
            public async Task Success()
            {
                // arrange
                const string primaryKey = "foo";
                SetupDeviceKeyLookup(this.stationEui, new[] { new IoTHubDeviceInfo { DevEUI = this.stationEui.ToString(), PrimaryKey = primaryKey } });
                SetupTwinResponse(this.stationEui, primaryKey, @$"{{ ""routerConfig"": {JsonUtil.Minify(LnsStationConfigurationTests.ValidStationConfiguration)} }}");

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
                SetupTwinResponse(this.stationEui, primaryKey, @$"{{ ""routerConfig"": {JsonUtil.Minify(LnsStationConfigurationTests.ValidStationConfiguration)} }}");

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
                SetupDeviceKeyLookup(this.stationEui, new IoTHubDeviceInfo { PrimaryKey = "foo", DevEUI = stationEui.ToString() });

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
                SetupDeviceKeyLookup(this.stationEui, new IoTHubDeviceInfo { PrimaryKey = "foo", DevEUI = stationEui.ToString() });
                SetupTwinResponse(this.stationEui, primaryKey, @$"{{ ""foo"": ""bar"" }}");

                // act + assert
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => this.sut.GetRouterConfigMessageAsync(this.stationEui, CancellationToken.None));
            }

            private void SetupTwinResponse(StationEui stationEui, string primaryKey, string json)
            {
                var deviceTwin = new Twin(new TwinProperties { Desired = new TwinCollection(json) });
                var deviceClientMock = new Mock<ILoRaDeviceClient>();
                deviceClientMock.Setup(dc => dc.GetTwinAsync()).Returns(Task.FromResult(deviceTwin));
                this.loRaDeviceFactoryMock.Setup(ldf => ldf.CreateDeviceClient(stationEui.ToString(), primaryKey))
                                          .Returns(deviceClientMock.Object);
            }

            private void SetupDeviceKeyLookup(StationEui stationEui, params IoTHubDeviceInfo[] ioTHubDeviceInfos) =>
                loRaDeviceApiServiceMock.Setup(ldas => ldas.SearchByDevEUIAsync(stationEui.ToString()))
                                        .Returns(Task.FromResult(new SearchDevicesResult(ioTHubDeviceInfos)));
        }
    }
}
