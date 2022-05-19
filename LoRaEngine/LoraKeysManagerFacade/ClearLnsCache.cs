// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;

    public class ClearLnsCache
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
            var edgeDevices = await this.edgeDeviceGetter.ListEdgeDevicesAsync(cancellationToken);
            var tasks = edgeDevices.Select(e => InvokeClearViaDirectMethodAsync(e, cancellationToken)).ToArray();

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
            try
            {
                var res = await this.serviceClient.InvokeDeviceMethodAsync(lnsId,
                                                                           LoraKeysManagerFacadeConstants.NetworkServerModuleId,
                                                                           new CloudToDeviceMethod(LoraKeysManagerFacadeConstants.ClearCacheMethodName),
                                                                           cancellationToken);
                if (HttpUtilities.IsSuccessStatusCode(res.Status))
                {
                    this.logger.LogInformation("Cache cleared for {gatewayID} via direct method", lnsId);
                }

                this.logger.LogError("Direct method call to {gatewayID} failed with {statusCode}. Response: {response}",
                                     lnsId,
                                     res.Status,
                                     res.GetPayloadAsJson());
            }
            catch (DeviceNotFoundException ex)
            {
                this.logger.LogError(ex,
                                     "Device named {gatewayID} did not exist or had no module named {moduleName}.",
                                     lnsId,
                                     LoraKeysManagerFacadeConstants.NetworkServerModuleId);
            }
        }
    }
}
