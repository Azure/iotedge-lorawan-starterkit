// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using Azure.Storage.Blobs;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.Logging;

    public class ConcentratorFwUpgradeFunction
    {
        internal const string CupsPropertyName = "cups";
        internal const string CupsFwUrlPropertyName = "fwUrl";
        private readonly RegistryManager registryManager;
        private readonly IAzureClientFactory<BlobServiceClient> azureClientFactory;
        private readonly ILogger<ConcentratorFwUpgradeFunction> logger;

        public ConcentratorFwUpgradeFunction(RegistryManager registryManager,
                                             IAzureClientFactory<BlobServiceClient> azureClientFactory,
                                             ILogger<ConcentratorFwUpgradeFunction> logger)
        {
            this.registryManager = registryManager;
            this.azureClientFactory = azureClientFactory;
            this.logger = logger;
        }
    }
}
