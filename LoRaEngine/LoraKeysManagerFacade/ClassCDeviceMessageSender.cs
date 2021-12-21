// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Newtonsoft.Json;

    public class ClassCDeviceMessageSender
    {
        private readonly RegistryManager registryManager;
        private readonly IServiceClient serviceClient;
        private readonly ILogger log;

        public ClassCDeviceMessageSender(RegistryManager registryManager, IServiceClient serviceClient, ILogger<ClassCDeviceMessageSender> log)
        {
            this.registryManager = registryManager;
            this.serviceClient = serviceClient;
            this.log = log;
        }

        /// <summary>
        /// Entry point function for sending a message to a class C device.
        /// </summary>
        [FunctionName(nameof(SendToClassCDevice))]
        public async Task<IActionResult> SendToClassCDevice(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            CancellationToken cancellationToken)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                this.log.LogError(ex, "Invalid version");
                return new BadRequestObjectResult(ex.Message);
            }

            return await RunSendToClassCDevice(req, cancellationToken);
        }

        private async Task<IActionResult> RunSendToClassCDevice(HttpRequest req, CancellationToken cancellationToken)
        {
            var devEUI = req.Query["DevEUI"];
            if (StringValues.IsNullOrEmpty(devEUI))
            {
                this.log.LogError("DevEUI missing in request");
                return new BadRequestObjectResult("DevEUI missing in request");
            }
            try
            {
                EUIValidator.ValidateDevEUI(devEUI);
            }
            catch (ArgumentException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            var fPort = req.Query["FPort"];
            if (StringValues.IsNullOrEmpty(fPort))
            {
                this.log.LogError("FPort missing in request");
                return new BadRequestObjectResult("FPort missing in request");
            }

            var payload = req.Query["RawPayload"];
            if (StringValues.IsNullOrEmpty(payload))
            {
                this.log.LogError("RawPayload missing in request");
                return new BadRequestObjectResult("RawPayload missing in request");
            }

            var twin = await this.registryManager.GetTwinAsync(devEUI, cancellationToken);
            if (twin != null)
            {
                if (string.Equals("c", twin.Properties?.Desired?.GetTwinPropertyStringSafe(LoraKeysManagerFacadeConstants.TwinProperty_ClassType), StringComparison.OrdinalIgnoreCase))
                {
                    var gatewayID = twin.Properties?.Reported?.GetTwinPropertyStringSafe(LoraKeysManagerFacadeConstants.TwinProperty_PreferredGatewayID);
                    if (string.IsNullOrEmpty(gatewayID))
                    {
                        gatewayID = twin.Properties?.Desired?.GetTwinPropertyStringSafe(LoraKeysManagerFacadeConstants.TwinProperty_GatewayID);
                    }

                    if (!string.IsNullOrEmpty(gatewayID))
                    {
                        return await SendMessageViaDirectMethodAsync(gatewayID, devEUI, payload);
                    }

                    // class c device that did not send a single upstream message
                    return new BadRequestObjectResult($"Gateway ID for device {devEUI} is unknown; cannot send a downstream message.");
                }

                // Not a class C device
                return new BadRequestObjectResult($"Provided device {devEUI} is not a class C device");
            }
            else
            {
                this.log.LogInformation($"Searching for {devEUI} returned 0 devices");
                return new NotFoundResult();
            }
        }

        private async Task<IActionResult> SendMessageViaDirectMethodAsync(
            string preferredGatewayID,
            string devEUI,
            string payload)
        {
            try
            {
                var method = new CloudToDeviceMethod(LoraKeysManagerFacadeConstants.CloudToDeviceMessageMethodName, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
                _ = method.SetPayloadJson(JsonConvert.SerializeObject(payload));

                var res = await this.serviceClient.InvokeDeviceMethodAsync(preferredGatewayID, LoraKeysManagerFacadeConstants.NetworkServerModuleId, method);
                if (IsSuccessStatusCode(res.Status))
                {
                    this.log.LogInformation("Direct method call to {gatewayID} and {devEUI} succeeded with {statusCode}", preferredGatewayID, devEUI, res.Status);

                    return new OkObjectResult(res);
                }

                this.log.LogError("Direct method call to {gatewayID} failed with {statusCode}. Response: {response}", preferredGatewayID, res.Status, res.GetPayloadAsJson());

                return new ObjectResult(res.GetPayloadAsJson())
                {
                    StatusCode = res.Status,
                };
            }
            catch (JsonSerializationException ex)
            {

                this.log.LogError(ex, "Failed to serialize the message payload");
                return new ObjectResult("Failed to serialize payload")
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }
            catch (IotHubException ex)
            {
                this.log.LogError(ex, "Failed to get device twin for devEUI {devEUI} from the IoT Hub", devEUI);
                return new ObjectResult("Failed to get device twin from the IoT Hub")
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }
        }

        private static bool IsSuccessStatusCode(int statusCode) => statusCode is >= 200 and <= 299;
    }
}
