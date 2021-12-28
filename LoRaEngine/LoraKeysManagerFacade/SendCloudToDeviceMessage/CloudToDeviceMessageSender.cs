// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    internal class CloudToDeviceMessageSender
    {
        private readonly IServiceClient serviceClient;
        private readonly ILogger log;

        public CloudToDeviceMessageSender(IServiceClient serviceClient, ILogger log)
        {
            this.serviceClient = serviceClient;
            this.log = log;
        }

        internal async Task<IActionResult> SendMessageViaCloudToDeviceMessageAsync(string devEUI, LoRaCloudToDeviceMessage c2dMessage)
        {
            try
            {
                using var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(c2dMessage)));
                message.MessageId = string.IsNullOrEmpty(c2dMessage.MessageId) ? Guid.NewGuid().ToString() : c2dMessage.MessageId;

                try
                {
                    await this.serviceClient.SendAsync(devEUI, message);
                }
                catch (IotHubException ex)
                {
                    this.log.LogError(ex, "Failed to send message to {devEUI} to IoT Hub", devEUI);
                    return new ObjectResult("Failed to send message to device to IoT Hub") { StatusCode = (int)HttpStatusCode.InternalServerError };
                }

                this.log.LogInformation("Sending cloud to device message to {devEUI} succeeded", devEUI);

                return new OkObjectResult(new SendCloudToDeviceMessageResult()
                {
                    DevEUI = devEUI,
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

        internal async Task<IActionResult> SendMessageViaDirectMethodAsync(
            string preferredGatewayID,
            string devEUI,
            LoRaCloudToDeviceMessage c2dMessage)
        {
            try
            {
                var method = new CloudToDeviceMethod(LoraKeysManagerFacadeConstants.CloudToDeviceMessageMethodName, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
                _ = method.SetPayloadJson(JsonConvert.SerializeObject(c2dMessage));

                var res = await this.serviceClient.InvokeDeviceMethodAsync(preferredGatewayID, LoraKeysManagerFacadeConstants.NetworkServerModuleId, method);
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
            catch (JsonSerializationException ex)
            {

                this.log.LogError(ex, "Failed to serialize C2D message {c2dmessage} to {devEUI}", devEUI);
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

        /// <summary>
        /// Gets if the http status code indicates success.
        /// </summary>
        private static bool IsSuccessStatusCode(int statusCode) => statusCode is >= 200 and <= 299;
    }
}
