// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoraKeysManagerFacade
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;

    public sealed class ClearLnsCache
    {
        private readonly IEdgeDeviceGetter edgeDeviceGetter;
        private readonly IServiceClient serviceClient;
        private readonly IChannelPublisher channelPublisher;
        private readonly ILogger<ClearLnsCache> logger;

        public ClearLnsCache(IEdgeDeviceGetter edgeDeviceGetter,
                             IServiceClient serviceClient,
                             IChannelPublisher channelPublisher,
                             ILogger<ClearLnsCache> logger)
        {
            this.edgeDeviceGetter = edgeDeviceGetter;
            this.serviceClient = serviceClient;
            this.channelPublisher = channelPublisher;
            this.logger = logger;
        }

        [FunctionName(nameof(ClearNetworkServerCache))]
        public async Task<IActionResult> ClearNetworkServerCache([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, CancellationToken cancellationToken)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            await ClearLnsCacheInternalAsync(cancellationToken);

            return new AcceptedResult();
        }

        internal async Task ClearLnsCacheInternalAsync(CancellationToken cancellationToken)
        {
            this.logger.LogInformation("Clearing device cache for all edge and Pub/Sub channel based Network Servers.");
            // Edge device discovery for invoking direct methods
            var edgeDevices = await this.edgeDeviceGetter.ListEdgeDevicesAsync(cancellationToken);
            if (this.logger.IsEnabled(LogLevel.Debug))
            {
                this.logger.LogDebug("Invoking clear cache direct method for following devices: {deviceList}", string.Join(',', edgeDevices));
            }
            var tasks = edgeDevices.Select(e => InvokeClearViaDirectMethodAsync(e, cancellationToken)).ToArray();
            // Publishing a single message for all cloud based LNSes
            await PublishClearMessageAsync();
            await Task.WhenAll(tasks);
        }

        internal async Task PublishClearMessageAsync()
        {
            await this.channelPublisher.PublishAsync(LoraKeysManagerFacadeConstants.ClearCacheMethodName, new LnsRemoteCall(RemoteCallKind.ClearCache, null));
            this.logger.LogInformation("Cache clear message published on Pub/Sub channel");
        }

        internal async Task InvokeClearViaDirectMethodAsync(string lnsId, CancellationToken cancellationToken)
        {
            //Reason why the yield is needed is to avoid any potential "synchronous" code that might fail the publishing of a message on the pub/sub channel
            await Task.Yield();
            var res = await this.serviceClient.InvokeDeviceMethodAsync(lnsId,
                                                                       Constants.NetworkServerModuleId,
                                                                       new CloudToDeviceMethod(LoraKeysManagerFacadeConstants.ClearCacheMethodName),
                                                                       cancellationToken);
            if (HttpUtilities.IsSuccessStatusCode(res.Status))
            {
                this.logger.LogInformation("Cache cleared for {gatewayID} via direct method", lnsId);
            }
            else
            {
                throw new InvalidOperationException($"Direct method call to {lnsId} failed with {res.Status}. Response: {res.GetPayloadAsJson()}");
            }
        }
    }
}
