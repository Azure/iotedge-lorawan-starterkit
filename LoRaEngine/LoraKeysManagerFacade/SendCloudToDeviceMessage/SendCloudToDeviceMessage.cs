// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Http function to sends cloud to device messages
    /// - For class C devices it sends the message through the preferred gateway
    /// - For other devices it adds to the device message queue
    /// </summary>
    public class SendCloudToDeviceMessage
    {
        internal const string NetworkServerModuleId = "LoRaWanNetworkSrvModule";
        internal const string CloudToDeviceMessageMethodName = "cloudtodevicemessage";
        internal const string TwinProperty_ClassType = "ClassType";
        internal const string TwinProperty_PreferredGatewayID = "PreferredGatewayID";
        internal const string TwinProperty_DevAddr = "DevAddr";
        internal const string TwinProperty_GatewayID = "GatewayID";
        private readonly ILoRaDeviceCacheStore cacheStore;
        private readonly RegistryManager registryManager;
        private readonly IServiceClient serviceClient;
        private readonly ILogger log;

        public SendCloudToDeviceMessage(ILoRaDeviceCacheStore cacheStore, RegistryManager registryManager, IServiceClient serviceClient, ILogger<SendCloudToDeviceMessage> log)
        {
            this.cacheStore = cacheStore;
            this.registryManager = registryManager;
            this.serviceClient = serviceClient;
            this.log = log;
        }

        [FunctionName("SendCloudToDeviceMessage")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "cloudtodevicemessage/{devEUI}")] HttpRequest req,
            string devEUI)
        {
            EUIValidator.ValidateDevEUI(devEUI);

            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("missing body");
            }

            var c2dMessage = JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(requestBody);
            c2dMessage.DevEUI = devEUI;

            return await this.SendCloudToDeviceMessageImplementationAsync(devEUI, c2dMessage);
        }

        public async Task<IActionResult> SendCloudToDeviceMessageImplementationAsync(string devEUI, LoRaCloudToDeviceMessage c2dMessage)
        {
            if (string.IsNullOrEmpty(devEUI))
            {
                return new BadRequestObjectResult($"Missing {nameof(devEUI)} value");
            }

            if (c2dMessage == null)
            {
                return new BadRequestObjectResult("Missing cloud to device message");
            }

            if (!c2dMessage.IsValid(out var errorMessage))
            {
                return new BadRequestObjectResult(errorMessage);
            }

            var cachedPreferredGateway = LoRaDevicePreferredGateway.LoadFromCache(this.cacheStore, devEUI);
            if (cachedPreferredGateway != null && !string.IsNullOrEmpty(cachedPreferredGateway.GatewayID))
            {
                return await this.SendMessageViaDirectMethodAsync(cachedPreferredGateway.GatewayID, devEUI, c2dMessage);
            }

            var queryText = $"SELECT * FROM devices WHERE deviceId = '{devEUI}'";
            var query = this.registryManager.CreateQuery(queryText, 1);
            if (query.HasMoreResults)
            {
                IEnumerable<Twin> deviceTwins;
                try
                {
                    deviceTwins = await query.GetNextAsTwinAsync();
                }
                catch (Exception ex)
                {
                    this.log.LogError(ex, "Failed to query devices with {query}", queryText);
                    return new ObjectResult("Failed to query devices") { StatusCode = (int)HttpStatusCode.InternalServerError };
                }

                var twin = deviceTwins.FirstOrDefault();

                if (twin != null)
                {
                    // the device must have a DevAddr
                    if (twin.Properties?.Desired?.GetTwinPropertyStringSafe(TwinProperty_DevAddr).Length == 0 && twin.Properties?.Reported?.GetTwinPropertyStringSafe(TwinProperty_DevAddr).Length == 0)
                    {
                        return new BadRequestObjectResult("Device DevAddr is unknown. Ensure the device has been correctly setup as a LoRa device and that it has connected to network at least once.");
                    }

                    if (string.Equals("c", twin.Properties?.Desired?.GetTwinPropertyStringSafe(TwinProperty_ClassType), StringComparison.InvariantCultureIgnoreCase))
                    {
                        var gatewayID = twin.Properties?.Reported?.GetTwinPropertyStringSafe(TwinProperty_PreferredGatewayID);
                        if (string.IsNullOrEmpty(gatewayID))
                        {
                            gatewayID = twin.Properties?.Desired?.GetTwinPropertyStringSafe(TwinProperty_GatewayID);
                        }

                        if (!string.IsNullOrEmpty(gatewayID))
                        {
                            // add it to cache (if it does not exist)
                            var preferredGateway = new LoRaDevicePreferredGateway(gatewayID, 0);
                            LoRaDevicePreferredGateway.SaveToCache(this.cacheStore, devEUI, preferredGateway, onlyIfNotExists: true);

                            return await this.SendMessageViaDirectMethodAsync(gatewayID, devEUI, c2dMessage);
                        }

                        // class c device that did not send a single upstream message
                        return new BadRequestObjectResult("Class C devices must sent at least one message upstream. None has been received");
                    }

                    // Not a class C device? Send message using sdk/queue
                    return await this.SendMessageViaCloudToDeviceMessageAsync(devEUI, c2dMessage);
                }
            }

            return new NotFoundObjectResult($"Device '{devEUI}' was not found");
        }

        async Task<IActionResult> SendMessageViaCloudToDeviceMessageAsync(string devEUI, LoRaCloudToDeviceMessage c2dMessage)
        {
            try
            {
                var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(c2dMessage)));
                message.MessageId = string.IsNullOrEmpty(c2dMessage.MessageId) ? Guid.NewGuid().ToString() : c2dMessage.MessageId;
                await this.serviceClient.SendAsync(devEUI, message);

                this.log.LogInformation("Sending cloud to device message to {devEUI} succeeded", devEUI);

                return new OkObjectResult(new SendCloudToDeviceMessageResult()
                {
                    DevEUI = devEUI,
                    MessageID = message.MessageId,
                    ClassType = "A",
                });
             }
            catch (Exception ex)
            {
                this.log.LogError(ex, "Failed to send message to {devEUI}", devEUI);
                return new ObjectResult("Failed to send message to device") { StatusCode = (int)HttpStatusCode.InternalServerError };
            }
        }

        async Task<IActionResult> SendMessageViaDirectMethodAsync(
            string preferredGatewayID,
            string devEUI,
            LoRaCloudToDeviceMessage c2dMessage)
        {
            try
            {
                var method = new CloudToDeviceMethod(CloudToDeviceMessageMethodName, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
                method.SetPayloadJson(JsonConvert.SerializeObject(c2dMessage));

                var res = await this.serviceClient.InvokeDeviceMethodAsync(preferredGatewayID, NetworkServerModuleId, method);
                if (IsSuccessStatusCode(res.Status))
                {
                    this.log.LogInformation("Direct method call to {gatewayID} and {devEUI} succeeded with {statusCode}", preferredGatewayID, devEUI, res.Status);

                    return new OkObjectResult(new SendCloudToDeviceMessageResult()
                    {
                        DevEUI = devEUI,
                        MessageID = c2dMessage.MessageId,
                        ClassType = "C",
                    });
                }

                this.log.LogError("Direct method call to {gatewayID} failed with {statusCode}. Response: {response}", preferredGatewayID, res.Status, res.GetPayloadAsJson());

                return new ObjectResult(res.GetPayloadAsJson())
                {
                    StatusCode = res.Status,
                };
            }
            catch (Exception ex)
            {
                this.log.LogError(ex, "Failed to send message to {devEUI}", devEUI);
                return new ObjectResult("Failed to send message to device")
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }
        }

        /// <summary>
        /// Gets if the http status code indicates success
        /// </summary>
        static bool IsSuccessStatusCode(int statusCode) => (statusCode >= 200) && (statusCode <= 299);
    }
}
