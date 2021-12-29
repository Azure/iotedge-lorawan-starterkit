// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class SendMessageToClassCDevice
    {
        private readonly RegistryManager registryManager;
        private readonly CloudToDeviceMessageSender cloudToDeviceMessageSender;
        private readonly ILogger log;

        public SendMessageToClassCDevice(RegistryManager registryManager, IServiceClient serviceClient, ILogger<SendMessageToClassCDevice> log)
        {
            this.registryManager = registryManager;
            this.log = log;
            this.cloudToDeviceMessageSender = new CloudToDeviceMessageSender(serviceClient, log);
        }

        /// <summary>
        /// Entry point function for sending a message to a class C device.
        /// </summary>
        [FunctionName(nameof(RunSendMessageToClassCDevice))]
        public async Task<IActionResult> RunSendMessageToClassCDevice(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "classcdevicemessage/{devEUI}")] string devEUI,
            HttpRequest req)
        {
            if (req is null)
            {
                return new BadRequestObjectResult("Request with message content is required");
            }

            try
            {
                VersionValidator.Validate(req);
                EUIValidator.ValidateDevEUI(devEUI);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }
            /*
            // parse query parameters
            var queryStrings = req.GetQueryParameterDictionary();

            // required arguments
            if (!queryStrings.TryGetValue("fport", out var fport))
            {
                return new BadRequestObjectResult("Fport is required");
            }

            if (!queryStrings.TryGetValue("payload", out var payload))
            {
                if (!queryStrings.TryGetValue("rawPayload", out payload))
                {
                    return new BadRequestObjectResult("Payload is required");
                }
            }

            var message = new LoRaCloudToDeviceMessage()
            {
                DevEUI = devEUI,
                Fport = (FramePort)fport,

            }
            */
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

            if (message.RawPayload == null && message.Payload == null)
            {
                return new BadRequestObjectResult("Payload is required");
            }

            message.DevEUI = devEUI;
            return await SendMessageToClassCDeviceAsync(message);
        }

        public async Task<IActionResult> SendMessageToClassCDeviceAsync(LoRaCloudToDeviceMessage message)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));

            var twin = await this.registryManager.GetTwinAsync(message.DevEUI);
            if (twin == null)
            {
                this.log.LogInformation($"Searching for {message.DevEUI} returned 0 devices");
                return new NotFoundResult();
            }

            if (!string.Equals("c", twin.Properties?.Desired?.GetTwinPropertyStringSafe(LoraKeysManagerFacadeConstants.TwinProperty_ClassType), StringComparison.OrdinalIgnoreCase))
            {
                // Not a class C device
                return new BadRequestObjectResult($"Provided device {message.DevEUI} is not a class C device");
            }

            var gatewayID = twin.Properties?.Reported?.GetTwinPropertyStringSafe(LoraKeysManagerFacadeConstants.TwinProperty_PreferredGatewayID);
            if (string.IsNullOrEmpty(gatewayID))
            {
                gatewayID = twin.Properties?.Desired?.GetTwinPropertyStringSafe(LoraKeysManagerFacadeConstants.TwinProperty_GatewayID);
            }

            if (string.IsNullOrEmpty(gatewayID))
            {
                // Gateway ID for the device is unknown
                return new BadRequestObjectResult($"Gateway ID for device {message.DevEUI} is unknown; cannot send a downstream message.");
            }

            return await this.cloudToDeviceMessageSender.SendMessageViaDirectMethodAsync(gatewayID, message.DevEUI, message);
        }
    }
}
