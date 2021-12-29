// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using global::LoraKeysManagerFacade;
    using global::LoRaTools.CommonAPI;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    public class SendMessageToClassCDeviceTest
    {
        private readonly Mock<IServiceClient> serviceClient;
        private readonly Mock<RegistryManager> registryManager;
        private readonly SendMessageToClassCDevice sendMessageToClassCDevice;

        public SendMessageToClassCDeviceTest()
        {
            this.serviceClient = new Mock<IServiceClient>(MockBehavior.Strict);
            this.registryManager = new Mock<RegistryManager>(MockBehavior.Strict);
            this.sendMessageToClassCDevice = new SendMessageToClassCDevice(this.registryManager.Object, this.serviceClient.Object, new NullLogger<SendMessageToClassCDevice>());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task When_DevEUI_Is_Missing_Should_Return_BadRequest(string devEUI)
        {
            var message = new LoRaCloudToDeviceMessage();
            var request = HttpRequestHelper.CreateRequest(JsonConvert.SerializeObject(message));

            var result = await this.sendMessageToClassCDevice.RunSendMessageToClassCDevice(devEUI, request);

            Assert.IsType<BadRequestObjectResult>(result);
            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }

        [Fact]
        public async Task When_Message_Is_Missing_Should_Return_BadRequest()
        {
            var actual = await this.sendMessageToClassCDevice.RunSendMessageToClassCDevice("0123456789", null);

            Assert.IsType<BadRequestObjectResult>(actual);
            this.serviceClient.VerifyAll();
            this.registryManager.VerifyAll();
        }
    }
}
