// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Host;
    using Microsoft.Extensions.Logging;

    public class SyncDevAddrCache
    {
        private LoRaDevAddrCache loRaDevAddrCache;
        private RegistryManager registryManager;

        public SyncDevAddrCache(LoRaDevAddrCache loRaDevAddrCache, RegistryManager registryManager)
        {
            this.loRaDevAddrCache = loRaDevAddrCache;
            this.registryManager = registryManager;
        }

        [FunctionName("SyncDevAddrCache")]
        public async Task Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogDebug($"{(myTimer.IsPastDue ? "The timer is past due" : "The timer is on schedule")}, Function last ran at {myTimer.ScheduleStatus.Last} Function next scheduled run at {myTimer.ScheduleStatus.Next})");

            await this.loRaDevAddrCache.PerformNeededSyncs(this.registryManager);
        }
    }
}
