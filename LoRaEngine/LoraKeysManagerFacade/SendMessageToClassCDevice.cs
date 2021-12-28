// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class SendMessageToClassCDevice
    {
        private readonly RegistryManager registryManager;
        private readonly IServiceClient serviceClient;
        private readonly ILogger log;

        public SendMessageToClassCDevice(RegistryManager registryManager, IServiceClient serviceClient, ILogger<SendMessageToClassCDevice> log)
        {
            this.registryManager = registryManager;
            this.serviceClient = serviceClient;
            this.log = log;
        }

        /// <summary>
        /// Entry point function for sending a message to a class C device.
        /// </summary>
        [FunctionName("SendMessageToClassCDevice")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "classcdevicemessage/{devEUI}")] HttpRequest req,
            string devEUI)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            try
            {
                VersionValidator.Validate(req);
                EUIValidator.ValidateDevEUI(devEUI);
            }
            catch (IncompatibleVersionException ex)
            {
                this.log.LogError(ex, "Invalid request version");
                return new BadRequestObjectResult(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("Missing request body");
            }

            var message = JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(requestBody);

            if (message == null)
            {
                return new BadRequestObjectResult("Missing message");
            }

            if (!message.IsValid(out var errorMessage))
            {
                return new BadRequestObjectResult(errorMessage);
            }

            message.DevEUI = devEUI;
            return await SendMessageToClassCDeviceAsync(devEUI, message);
        }

        private async Task<IActionResult> SendMessageToClassCDeviceAsync(string devEUI, LoRaCloudToDeviceMessage message)
        {
            var twin = await this.registryManager.GetTwinAsync(devEUI);
            if (twin == null)
            {
                this.log.LogInformation($"Searching for {devEUI} returned 0 devices");
                return new NotFoundResult();
            }

            if (!string.Equals("c", twin.Properties?.Desired?.GetTwinPropertyStringSafe(LoraKeysManagerFacadeConstants.TwinProperty_ClassType), StringComparison.OrdinalIgnoreCase))
            {
                // Not a class C device
                return new BadRequestObjectResult($"Provided device {devEUI} is not a class C device");
            }

            var gatewayID = twin.Properties?.Reported?.GetTwinPropertyStringSafe(LoraKeysManagerFacadeConstants.TwinProperty_PreferredGatewayID);
            if (string.IsNullOrEmpty(gatewayID))
            {
                gatewayID = twin.Properties?.Desired?.GetTwinPropertyStringSafe(LoraKeysManagerFacadeConstants.TwinProperty_GatewayID);
            }

            if (string.IsNullOrEmpty(gatewayID))
            {
                // Gateway ID for the device is unknown
                return new BadRequestObjectResult($"Gateway ID for device {devEUI} is unknown; cannot send a downstream message.");
            }

            return await SendMessageViaDirectMethodAsync(gatewayID, devEUI, message);
        }

        private async Task<IActionResult> SendMessageViaDirectMethodAsync(
            string preferredGatewayID,
            string devEUI,
            LoRaCloudToDeviceMessage message)
        {
            try
            {
                var method = new CloudToDeviceMethod(LoraKeysManagerFacadeConstants.CloudToDeviceMessageMethodName, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
                _ = method.SetPayloadJson(JsonConvert.SerializeObject(message));

                var res = await this.serviceClient.InvokeDeviceMethodAsync(preferredGatewayID, LoraKeysManagerFacadeConstants.NetworkServerModuleId, method);
                if (IsSuccessStatusCode(res.Status))
                {
                    this.log.LogInformation("Direct method call to {gatewayID} and {devEUI} succeeded with {statusCode}", preferredGatewayID, devEUI, res.Status);
                    return new OkObjectResult(new SendCloudToDeviceMessageResult()
                    {
                        DevEUI = devEUI,
                        MessageID = message.MessageId,
                        ClassType = "C"
                    });
                }

                this.log.LogError("Direct method call to {gatewayID} failed with {statusCode}. Response: {response}", preferredGatewayID, res.Status, res.GetPayloadAsJson());

                return new ObjectResult(res.GetPayloadAsJson())
                {
                    StatusCode = res.Status,
                };
            }
            catch (JsonSerializationException ex)
            {
                this.log.LogError(ex, "Failed to serialize the message to devEUI {devEUI}", devEUI);
                return new ObjectResult("Failed to serialize message")
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
