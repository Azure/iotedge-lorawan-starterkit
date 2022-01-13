// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using Azure.Storage.Blobs;
    using global::LoraKeysManagerFacade;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;

    public class ConcentratorFirmwareFunctionTests
    {
        private readonly Mock<RegistryManager> registryManager;
        private readonly Mock<IAzureClientFactory<BlobServiceClient>> azureClientFactory;
        private readonly ConcentratorFirmwareFunction concentratorFirmware;

        public ConcentratorFirmwareFunctionTests()
        {
            this.registryManager = new Mock<RegistryManager>();
            this.azureClientFactory = new Mock<IAzureClientFactory<BlobServiceClient>>();
            this.concentratorFirmware = new ConcentratorFirmwareFunction(registryManager.Object, azureClientFactory.Object, NullLogger<ConcentratorFirmwareFunction>.Instance);
        }
    }
}
