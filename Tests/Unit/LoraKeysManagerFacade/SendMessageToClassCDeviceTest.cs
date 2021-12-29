// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using global::LoraKeysManagerFacade;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;

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
    }
}
