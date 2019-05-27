// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// This Cache Manager class is used to perform synchronisation at function startup.
    /// </summary>
    class DevAddressCacheManager : IHostedService
    {
        private readonly RegistryManager registryManager;
        private readonly ILoRaDeviceCacheStore cacheStore;
        private readonly ILogger<DevAddressCacheManager> logger;

        public DevAddressCacheManager(RegistryManager registryManager, ILoRaDeviceCacheStore cacheStore, ILogger<DevAddressCacheManager> logger)
        {
            this.registryManager = registryManager;
            this.cacheStore = cacheStore;
            this.logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            LoRaDevAddrCache loRaDevAddrCache = new LoRaDevAddrCache(this.cacheStore, this.registryManager, this.logger, null);
            await Task.FromResult(0);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
