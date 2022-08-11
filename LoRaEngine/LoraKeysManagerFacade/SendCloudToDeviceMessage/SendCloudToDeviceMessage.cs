// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.CommonAPI;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Http function to sends cloud to device messages
    /// - For class C devices it sends the message through the preferred gateway
    /// - For other devices it adds to the device message queue.
    /// </summary>
    public class SendCloudToDeviceMessage
    {
        private readonly ILoRaDeviceCacheStore cacheStore;
        private readonly IDeviceRegistryManager registryManager;
        private readonly IServiceClient serviceClient;
        private readonly IEdgeDeviceGetter edgeDeviceGetter;
        private readonly IChannelPublisher channelPublisher;
        private readonly ILogger log;

        public SendCloudToDeviceMessage(ILoRaDeviceCacheStore cacheStore,
                                        IDeviceRegistryManager registryManager,
                                        IServiceClient serviceClient,
                                        IEdgeDeviceGetter edgeDeviceGetter,
                                        IChannelPublisher channelPublisher,
                                        ILogger<SendCloudToDeviceMessage> log)
        {
            this.cacheStore = cacheStore;
            this.registryManager = registryManager;
            this.serviceClient = serviceClient;
            this.edgeDeviceGetter = edgeDeviceGetter;
            this.channelPublisher = channelPublisher;
            this.log = log;
        }

        [FunctionName("SendCloudToDeviceMessage")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "cloudtodevicemessage/{devEUI}")] HttpRequest req,
            string devEUI,
            CancellationToken cancellationToken)
        {
            DevEui parsedDevEui;

            try
            {
                VersionValidator.Validate(req);
                if (!DevEui.TryParse(devEUI, EuiParseOptions.ForbidInvalid, out parsedDevEui))
                {
                    return new BadRequestObjectResult("Dev EUI is invalid.");
                }
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            using var deviceScope = this.log.BeginDeviceScope(parsedDevEui);

            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("missing body");
            }

            var c2dMessage = JsonConvert.DeserializeObject<LoRaCloudToDeviceMessage>(requestBody);
            c2dMessage.DevEUI = parsedDevEui;

            return await SendCloudToDeviceMessageImplementationAsync(parsedDevEui, c2dMessage, cancellationToken);
        }

        public async Task<IActionResult> SendCloudToDeviceMessageImplementationAsync(DevEui devEUI, LoRaCloudToDeviceMessage c2dMessage, CancellationToken cancellationToken)
        {
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
                return await SendMessageViaDirectMethodOrPubSubAsync(cachedPreferredGateway.GatewayID, devEUI, c2dMessage, cancellationToken);
            }

            var query = this.registryManager.FindDeviceByDevEUI(devEUI);
            if (query.HasMoreResults)
            {
                IEnumerable<IDeviceTwin> deviceTwins;
                try
                {
                    deviceTwins = await query.GetNextPageAsync();
                }
                catch (IotHubException ex)
                {
                    this.log.LogError(ex, "Failed to query devices");
                    return new ObjectResult("Failed to query devices") { StatusCode = (int)HttpStatusCode.InternalServerError };
                }

                var twin = deviceTwins.FirstOrDefault();

                if (twin != null)
                {
                    var desiredReader = new TwinPropertiesReader(twin.Properties.Desired, this.log);
                    var reportedReader = new TwinPropertiesReader(twin.Properties.Reported, this.log);

                    // the device must have a DevAddr
                    if (!desiredReader.TryRead(TwinPropertiesConstants.DevAddr, out DevAddr _) && !reportedReader.TryRead(TwinPropertiesConstants.DevAddr, out DevAddr _))
                    {
                        return new BadRequestObjectResult("Device DevAddr is unknown. Ensure the device has been correctly setup as a LoRa device and that it has connected to network at least once.");
                    }

                    if (desiredReader.TryRead(TwinPropertiesConstants.ClassType, out string deviceClass) && string.Equals("c", deviceClass, StringComparison.OrdinalIgnoreCase))
                    {
                        if ((reportedReader.TryRead(TwinPropertiesConstants.PreferredGatewayID, out string gatewayID)
                            || desiredReader.TryRead(TwinPropertiesConstants.GatewayID, out gatewayID))
                            && !string.IsNullOrEmpty(gatewayID))
                        {
                            // add it to cache (if it does not exist)
                            var preferredGateway = new LoRaDevicePreferredGateway(gatewayID, 0);
                            _ = LoRaDevicePreferredGateway.SaveToCache(this.cacheStore, devEUI, preferredGateway, onlyIfNotExists: true);

                            return await SendMessageViaDirectMethodOrPubSubAsync(gatewayID, devEUI, c2dMessage, cancellationToken);
                        }

                        // class c device that did not send a single upstream message
                        return new ObjectResult("Class C devices must sent at least one message upstream. None has been received")
                        {
                            StatusCode = (int)HttpStatusCode.InternalServerError
                        };
                    }

                    // Not a class C device? Send message using sdk/queue
                    return await SendMessageViaCloudToDeviceMessageAsync(devEUI, c2dMessage);
                }
            }

            return new NotFoundObjectResult($"Device '{devEUI}' was not found");
        }

        private async Task<IActionResult> SendMessageViaCloudToDeviceMessageAsync(DevEui devEUI, LoRaCloudToDeviceMessage c2dMessage)
        {
            try
            {
                using var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(c2dMessage)));
                message.MessageId = string.IsNullOrEmpty(c2dMessage.MessageId) ? Guid.NewGuid().ToString() : c2dMessage.MessageId;

                // class a devices only listen for 1-2 seconds, so we send to a queue on the device - we don't care about this for redis
                try
                {
                    await this.serviceClient.SendAsync(devEUI.ToString(), message);
                }
                catch (IotHubException ex)
                {
                    this.log.LogError(ex, "Failed to send message to {devEUI} to IoT Hub", devEUI);
                    return new ObjectResult("Failed to send message to device to IoT Hub") { StatusCode = (int)HttpStatusCode.InternalServerError };
                }

                this.log.LogInformation("Sending cloud to device message to {devEUI} succeeded", devEUI);

                return new OkObjectResult(new SendCloudToDeviceMessageResult()
                {
                    DevEui = devEUI,
                    MessageID = message.MessageId,
                    ClassType = "A",
                });
            }
            catch (JsonSerializationException ex)
            {
                this.log.LogError(ex, "Failed to serialize message {c2dmessage} for device {devEUI} to IoT Hub", c2dMessage, devEUI);
                return new ObjectResult("Failed to serialize c2d message to device to IoT Hub") { StatusCode = (int)HttpStatusCode.InternalServerError };
            }
        }

        private async Task<IActionResult> SendMessageViaDirectMethodOrPubSubAsync(
            string preferredGatewayID,
            DevEui devEUI,
            LoRaCloudToDeviceMessage c2dMessage,
            CancellationToken cancellationToken)
        {
            try
            {
                var method = new CloudToDeviceMethod(LoraKeysManagerFacadeConstants.CloudToDeviceMessageMethodName, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
                var jsonContent = JsonConvert.SerializeObject(c2dMessage);
                _ = method.SetPayloadJson(jsonContent);

                if (await edgeDeviceGetter.IsEdgeDeviceAsync(preferredGatewayID, cancellationToken))
                {
                    var res = await this.serviceClient.InvokeDeviceMethodAsync(preferredGatewayID, Constants.NetworkServerModuleId, method, cancellationToken);
                    if (HttpUtilities.IsSuccessStatusCode(res.Status))
                    {
                        this.log.LogInformation("Direct method call to {gatewayID} and {devEUI} succeeded with {statusCode}", preferredGatewayID, devEUI, res.Status);

                        return new OkObjectResult(new SendCloudToDeviceMessageResult()
                        {
                            DevEui = devEUI,
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
                else
                {
                    await this.channelPublisher.PublishAsync(preferredGatewayID, new LnsRemoteCall(RemoteCallKind.CloudToDeviceMessage, jsonContent));
                    this.log.LogInformation("C2D message to {gatewayID} and {devEUI} published to Redis queue", preferredGatewayID, devEUI);

                    return new OkObjectResult(new SendCloudToDeviceMessageResult()
                    {
                        DevEui = devEUI,
                        MessageID = c2dMessage.MessageId,
                        ClassType = "C",
                    });
                }
            }
            catch (JsonSerializationException ex)
            {

                this.log.LogError(ex, "Failed to serialize C2D message {c2dmessage} to {devEUI}", JsonConvert.SerializeObject(c2dMessage), devEUI);
                return new ObjectResult("Failed serialize C2D Message")
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }
            catch (IotHubException ex)
            {
                this.log.LogError(ex, "Failed to send message for {devEUI} to the IoT Hub", devEUI);
                return new ObjectResult("Failed to send message for device to the iot Hub")
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }
        }
    }
}
