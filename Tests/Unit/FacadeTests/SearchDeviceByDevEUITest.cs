// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.FacadeTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade;
    using LoRaTools.CommonAPI;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
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
            var searchDeviceByDevEUI = new SearchDeviceByDevEUI(registryManager.Object);
            var result = await searchDeviceByDevEUI.GetDeviceByDevEUI(ctx.Request, NullLogger.Instance);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task When_Version_Is_Missing_Should_Return_BadResult()
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.QueryString = new QueryString($"?devEUI=193123");
            var registryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);
            var searchDeviceByDevEUI = new SearchDeviceByDevEUI(registryManager.Object);
            var result = await searchDeviceByDevEUI.GetDeviceByDevEUI(ctx.Request, NullLogger.Instance);
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
            var searchDeviceByDevEUI = new SearchDeviceByDevEUI(registryManager.Object);
            var result = await searchDeviceByDevEUI.GetDeviceByDevEUI(ctx.Request, NullLogger.Instance);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task When_Device_Is_Not_Found_Should_Returns_NotFound()
        {
            const string devEUI = "13213123212131";
            var ctx = new DefaultHttpContext();
            ctx.Request.QueryString = new QueryString($"?devEUI={devEUI}&{ApiVersion.QueryStringParamName}={ApiVersion.LatestVersion}");

            var registryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);
            registryManager.Setup(x => x.GetDeviceAsync(devEUI))
                .ReturnsAsync((IDevice)null);

            var searchDeviceByDevEUI = new SearchDeviceByDevEUI(registryManager.Object);

            var result = await searchDeviceByDevEUI.GetDeviceByDevEUI(ctx.Request, NullLogger.Instance);
            Assert.IsType<NotFoundObjectResult>(result);

            registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Found_Should_Returns_Device_Information()
        {
            const string devEUI = "13213123212131";
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            var ctx = new DefaultHttpContext();
            ctx.Request.QueryString = new QueryString($"?devEUI={devEUI}&{ApiVersion.QueryStringParamName}={ApiVersion.LatestVersion}");

            var registryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);

            registryManager.Setup(x => x.GetDeviceAsync(devEUI))
                .ReturnsAsync((string x) =>
                {
                    var deviceMock = new Mock<IDevice>(MockBehavior.Strict);

                    deviceMock.SetupGet(c => c.DeviceId).Returns(devEUI);
                    deviceMock.SetupGet(c => c.PrimaryKey).Returns(primaryKey);
                    deviceMock.SetupGet(c => c.DeviceId).Returns(primaryKey);

                    return deviceMock.Object;
                });

            var searchDeviceByDevEUI = new SearchDeviceByDevEUI(registryManager.Object);

            var result = await searchDeviceByDevEUI.GetDeviceByDevEUI(ctx.Request, NullLogger.Instance);
            Assert.IsType<OkObjectResult>(result);
            Assert.IsType<List<IoTHubDeviceInfo>>(((OkObjectResult)result).Value);
            var devices = (List<IoTHubDeviceInfo>)((OkObjectResult)result).Value;
            Assert.Single(devices);
            Assert.Equal(devEUI, devices[0].DevEUI);
            Assert.Equal(primaryKey, devices[0].PrimaryKey);

            registryManager.VerifyAll();
        }
    }
}
