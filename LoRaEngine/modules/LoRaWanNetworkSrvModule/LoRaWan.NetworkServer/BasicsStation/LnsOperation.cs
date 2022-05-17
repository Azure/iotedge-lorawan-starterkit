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
    using Microsoft.Extensions.Logging;

    internal interface ILnsRemoteCall
    {
        Task<HttpStatusCode> ClearCacheAsync();
        Task<HttpStatusCode> CloseConnectionAsync(string json, CancellationToken cancellationToken);
        Task<HttpStatusCode> SendCloudToDeviceMessageAsync(string json, CancellationToken cancellationToken);
    }

    internal sealed class LnsRemoteCall : ILnsRemoteCall
    {
        internal const string ClosedConnectionLog = "Device connection was closed ";
        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private readonly NetworkServerConfiguration networkServerConfiguration;
        private readonly IClassCDeviceMessageSender classCDeviceMessageSender;
        private readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        private readonly ILogger<LnsRemoteCall> logger;
        private readonly Counter<int> forceClosedConnections;

        public LnsRemoteCall(NetworkServerConfiguration networkServerConfiguration,
                            IClassCDeviceMessageSender classCDeviceMessageSender,
                            ILoRaDeviceRegistry loRaDeviceRegistry,
                            ILogger<LnsRemoteCall> logger,
                            Meter meter)
        {
            this.networkServerConfiguration = networkServerConfiguration;
            this.classCDeviceMessageSender = classCDeviceMessageSender;
            this.loRaDeviceRegistry = loRaDeviceRegistry;
            this.logger = logger;
            this.forceClosedConnections = meter.CreateCounter<int>(MetricRegistry.ForceClosedClientConnections);
        }

        public async Task<HttpStatusCode> SendCloudToDeviceMessageAsync(string json, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(json))
            {
                ReceivedLoRaCloudToDeviceMessage c2d;

                try
                {
                    c2d = JsonSerializer.Deserialize<ReceivedLoRaCloudToDeviceMessage>(json, JsonSerializerOptions);
                }
                catch (JsonException ex)
                {
                    this.logger.LogError(ex, $"Impossible to parse Json for c2d message, error: '{ex}'");
                    return HttpStatusCode.BadRequest;
                }

                using var scope = this.logger.BeginDeviceScope(c2d.DevEUI);
                this.logger.LogDebug($"received cloud to device message from direct method: {json}");

                if (await this.classCDeviceMessageSender.SendAsync(c2d, cancellationToken))
                {
                    return HttpStatusCode.OK;
                }
            }

            return HttpStatusCode.BadRequest;
        }

        public async Task<HttpStatusCode> CloseConnectionAsync(string json, CancellationToken cancellationToken)
        {
            ReceivedLoRaCloudToDeviceMessage c2d;

            try
            {
                c2d = JsonSerializer.Deserialize<ReceivedLoRaCloudToDeviceMessage>(json, JsonSerializerOptions);
            }
            catch (JsonException ex)
            {
                this.logger.LogError(ex, "Unable to parse Json when attempting to close the connection.");
                return HttpStatusCode.BadRequest;
            }

            if (c2d == null)
            {
                this.logger.LogError("Missing payload when attempting to close the connection.");
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
            await loRaDevice.CloseConnectionAsync(cancellationToken, force: true);

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
