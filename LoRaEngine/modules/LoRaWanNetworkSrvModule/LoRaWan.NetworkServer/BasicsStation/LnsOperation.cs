// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System.Diagnostics.Metrics;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;

    internal interface ILnsOperation
    {
        Task<HttpStatusCode> ClearCacheAsync();
        Task<HttpStatusCode> CloseConnectionAsync(MethodRequest methodRequest);
        Task<HttpStatusCode> SendCloudToDeviceMessageAsync(MethodRequest methodRequest);
    }

    internal sealed class LnsOperation : ILnsOperation
    {
        internal const string ClosedConnectionLog = "Device connection was closed ";

        private readonly NetworkServerConfiguration networkServerConfiguration;
        private readonly IClassCDeviceMessageSender classCDeviceMessageSender;
        private readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        private readonly ILogger<LnsOperation> logger;
        private readonly Counter<int> forceClosedConnections;

        public LnsOperation(NetworkServerConfiguration networkServerConfiguration,
                            IClassCDeviceMessageSender classCDeviceMessageSender,
                            ILoRaDeviceRegistry loRaDeviceRegistry,
                            ILogger<LnsOperation> logger,
                            Meter meter)
        {
            this.networkServerConfiguration = networkServerConfiguration;
            this.classCDeviceMessageSender = classCDeviceMessageSender;
            this.loRaDeviceRegistry = loRaDeviceRegistry;
            this.logger = logger;
            this.forceClosedConnections = meter.CreateCounter<int>(MetricRegistry.ForceClosedClientConnections);
        }

        public async Task<HttpStatusCode> SendCloudToDeviceMessageAsync(MethodRequest methodRequest)
        {
            if (!string.IsNullOrEmpty(methodRequest.DataAsJson))
            {
                ReceivedLoRaCloudToDeviceMessage c2d = null;

                try
                {
                    c2d = JsonSerializer.Deserialize<ReceivedLoRaCloudToDeviceMessage>(methodRequest.DataAsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException ex)
                {
                    this.logger.LogError($"Impossible to parse Json for c2d message for device {c2d?.DevEUI}, error: {ex}");
                    return HttpStatusCode.BadRequest;
                }

                using var scope = this.logger.BeginDeviceScope(c2d.DevEUI);
                this.logger.LogDebug($"received cloud to device message from direct method: {methodRequest.DataAsJson}");

                using var cts = methodRequest.ResponseTimeout.HasValue ? new CancellationTokenSource(methodRequest.ResponseTimeout.Value) : null;

                if (await this.classCDeviceMessageSender.SendAsync(c2d, cts?.Token ?? CancellationToken.None))
                {
                    return HttpStatusCode.OK;
                }
            }

            return HttpStatusCode.BadRequest;
        }

        public async Task<HttpStatusCode> CloseConnectionAsync(MethodRequest methodRequest)
        {
            ReceivedLoRaCloudToDeviceMessage c2d = null;

            try
            {
                c2d = methodRequest.DataAsJson is { } json ? JsonSerializer.Deserialize<ReceivedLoRaCloudToDeviceMessage>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
            }
            catch (JsonException ex)
            {
                this.logger.LogError(ex, "Unable to parse Json for direct method '{MethodName}' for device '{DevEui}', message id '{MessageId}'", methodRequest.Name, c2d?.DevEUI, c2d?.MessageId);
                return HttpStatusCode.BadRequest;
            }

            if (c2d == null)
            {
                this.logger.LogError("Missing payload for direct method '{MethodName}'", methodRequest.Name);
                return HttpStatusCode.BadRequest;
            }

            if (c2d.DevEUI == null)
            {
                this.logger.LogError("DevEUI missing, cannot identify device to close connection for; message Id '{MessageId}'", c2d.MessageId);
                return HttpStatusCode.BadRequest;
            }

            using var scope = this.logger.BeginDeviceScope(c2d.DevEUI);

            var loRaDevice = await this.loRaDeviceRegistry.GetDeviceByDevEUIAsync(c2d.DevEUI.Value);
            if (loRaDevice == null)
            {
                this.logger.LogError("Could not retrieve LoRa device; message id '{MessageId}'", c2d.MessageId);
                return HttpStatusCode.NotFound;
            }

            loRaDevice.IsConnectionOwner = false;
            using var cts = methodRequest.ResponseTimeout is { } timeout ? new CancellationTokenSource(timeout) : null;
            await loRaDevice.CloseConnectionAsync(cts?.Token ?? CancellationToken.None, force: true);

            this.logger.LogInformation(ClosedConnectionLog + "from gateway with id '{GatewayId}', message id '{MessageId}'", this.networkServerConfiguration.GatewayID, c2d.MessageId);
            this.forceClosedConnections.Add(1);

            return HttpStatusCode.OK;
        }

        public async Task<HttpStatusCode> ClearCacheAsync()
        {
            await this.loRaDeviceRegistry.ResetDeviceCacheAsync();
            return HttpStatusCode.OK;
        }
    }
}
