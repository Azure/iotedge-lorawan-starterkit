// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using global::LoraKeysManagerFacade;
    using global::LoRaTools;
    using global::LoRaTools.CommonAPI;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    // Ensure tests don't run in parallel since LoRaRegistryManager is shared
    [Collection("LoraKeysManagerFacade.Test")]
    public class SearchDeviceByDevEUITest
    {
        private const string PrimaryKey = "ABCDEFGH1234567890";

        [Fact]
        public async Task When_Query_String_Is_Not_Found_Should_Return_BadResult()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.QueryString = new QueryString($"?{ApiVersion.QueryStringParamName}={ApiVersion.Version_2019_02_12_Preview.Version}");
            var registryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);
            var searchDeviceByDevEUI = SetupSubject(registryManager.Object);
            var result = await searchDeviceByDevEUI.GetDeviceByDevEUI(ctx.Request);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task When_Version_Is_Missing_Should_Return_BadResult()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.QueryString = new QueryString($"?devEUI=193123");
            var registryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);
            var searchDeviceByDevEUI = SetupSubject(registryManager.Object);
            var result = await searchDeviceByDevEUI.GetDeviceByDevEUI(ctx.Request);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [InlineData("2018-12-16-preview")]
        [InlineData("ree")]
        public async Task When_Version_Is_Not_Supported_Should_Return_BadResult(string version)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.QueryString = new QueryString($"?devEUI=193123&{ApiVersion.QueryStringParamName}={version}");
            var registryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);
            var searchDeviceByDevEUI = SetupSubject(registryManager.Object);
            var result = await searchDeviceByDevEUI.GetDeviceByDevEUI(ctx.Request);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [InlineData("N")]
        [InlineData("G")]
        public async Task When_Device_Is_Not_Found_Should_Returns_NotFound(string format)
        {
            var devEUI = new DevEui(13213123212131);
            var ctx = new DefaultHttpContext();
            ctx.Request.QueryString = new QueryString($"?devEUI={devEUI.ToString(format, null)}&{ApiVersion.QueryStringParamName}={ApiVersion.LatestVersion}");

            var registryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);
            registryManager.Setup(x => x.GetDevicePrimaryKeyAsync(devEUI.ToString()))
                .ReturnsAsync((string)null);

            var searchDeviceByDevEUI = SetupSubject(registryManager.Object);

            var result = await searchDeviceByDevEUI.GetDeviceByDevEUI(ctx.Request);
            Assert.IsType<NotFoundResult>(result);

            registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Found_Should_Returns_Device_Information()
        {
            // arrange
            var devEui = new DevEui(13213123212131);
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            var (registryManager, request) = SetupIotHubQuery(devEui.ToString(), PrimaryKey);
            var searchDeviceByDevEUI = SetupSubject(registryManager.Object);

            // act
            var result = await searchDeviceByDevEUI.GetDeviceByDevEUI(request);

            // assert
            var okObjectResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(JsonConvert.SerializeObject(new { DevEUI = devEui.ToString(), PrimaryKey = primaryKey }), JsonConvert.SerializeObject(okObjectResult.Value));
            registryManager.VerifyAll();
        }

        [Fact]
        public async Task Complies_With_Contract()
        {
            // arrange
            var devEui = SearchByDevEuiContract.Eui;
            var primaryKey = SearchByDevEuiContract.PrimaryKey;
            var (registryManager, request) = SetupIotHubQuery(devEui, primaryKey);
            var searchDeviceByDevEUI = SetupSubject(registryManager.Object);

            // act
            var result = await searchDeviceByDevEUI.GetDeviceByDevEUI(request);

            // assert
            var okObjectResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(SearchByDevEuiContract.Response, JsonConvert.SerializeObject(okObjectResult.Value));
            registryManager.VerifyAll();
        }

        private static (Mock<IDeviceRegistryManager>, HttpRequest) SetupIotHubQuery(string devEui, string primaryKey)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.QueryString = new QueryString($"?devEUI={devEui}&{ApiVersion.QueryStringParamName}={ApiVersion.LatestVersion}");

            var registryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);

            registryManager.Setup(x => x.GetDevicePrimaryKeyAsync(devEui))
                           .ReturnsAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(primaryKey)));

            return (registryManager, ctx.Request);
        }

        private static SearchDeviceByDevEUI SetupSubject(IDeviceRegistryManager registryManager) =>
            new SearchDeviceByDevEUI(registryManager, NullLogger<SearchDeviceByDevEUI>.Instance);
    }
}
