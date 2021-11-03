// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.ModuleConnection
{
    using Microsoft.Azure.Devices.Client;
    using System.Threading.Tasks;

    public class LoRaModuleClientFactory : ILoRaModuleClientFactory
    {
        private readonly ITransportSettings[] settings;

        public LoRaModuleClientFactory(ITransportSettings[] settings)
        {
            this.settings = settings;
        }

        public async Task<ILoraModuleClient> CreateAsync()
        {
            return new LoRaModuleClient(await ModuleClient.CreateFromEnvironmentAsync(this.settings));
        }
    }
}
