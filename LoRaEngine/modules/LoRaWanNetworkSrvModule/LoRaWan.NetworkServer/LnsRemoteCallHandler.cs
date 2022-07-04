// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Diagnostics.Metrics;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using Microsoft.Extensions.Logging;

    internal interface ILnsRemoteCallHandler
    {
        Task<HttpStatusCode> ExecuteAsync(LnsRemoteCall lnsRemoteCall, CancellationToken cancellationToken);
    }

    internal sealed class LnsRemoteCallHandler : ILnsRemoteCallHandler
    {
        internal const string ClosedConnectionLog = "Device connection was closed ";
        private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private readonly NetworkServerConfiguration networkServerConfiguration;
        private readonly IClassCDeviceMessageSender classCDeviceMessageSender;
        private readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        private readonly ILogger<LnsRemoteCallHandler> logger;
        private readonly Counter<int> forceClosedConnections;

        public LnsRemoteCallHandler(NetworkServerConfiguration networkServerConfiguration,
                                    IClassCDeviceMessageSender classCDeviceMessageSender,
                                    ILoRaDeviceRegistry loRaDeviceRegistry,
                                    ILogger<LnsRemoteCallHandler> logger,
                                    Meter meter)
        {
            this.networkServerConfiguration = networkServerConfiguration;
            this.classCDeviceMessageSender = classCDeviceMessageSender;
            this.loRaDeviceRegistry = loRaDeviceRegistry;
            this.logger = logger;
            this.forceClosedConnections = meter.CreateCounter<int>(MetricRegistry.ForceClosedClientConnections);
        }

        public Task<HttpStatusCode> ExecuteAsync(LnsRemoteCall lnsRemoteCall, CancellationToken cancellationToken)
        {
            return lnsRemoteCall.Kind switch
            {
                RemoteCallKind.CloudToDeviceMessage => SendCloudToDeviceMessageAsync(lnsRemoteCall.JsonData, cancellationToken),
                RemoteCallKind.ClearCache => ClearCacheAsync(),
                RemoteCallKind.CloseConnection => CloseConnectionAsync(lnsRemoteCall.JsonData, cancellationToken),
                _ => throw new System.NotImplementedException(),
            };
        }

        private async Task<HttpStatusCode> SendCloudToDeviceMessageAsync(string json, CancellationToken cancellationToken)
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
                    return HttpStatusCode.OK;
            }

            return HttpStatusCode.BadRequest;
        }

        private async Task<HttpStatusCode> CloseConnectionAsync(string json, CancellationToken cancellationToken)
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

        private async Task<HttpStatusCode> ClearCacheAsync()
        {
            await this.loRaDeviceRegistry.ResetDeviceCacheAsync();
            return HttpStatusCode.OK;
        }
    }
}
