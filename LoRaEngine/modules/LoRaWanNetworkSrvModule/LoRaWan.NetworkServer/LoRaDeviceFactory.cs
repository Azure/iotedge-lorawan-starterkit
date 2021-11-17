// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;

    public class LoRaDeviceFactory : ILoRaDeviceFactory
    {
        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDataRequestHandler dataRequestHandler;
        private readonly ILoRaDeviceClientConnectionManager connectionManager;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<LoRaDeviceFactory> logger;

        public LoRaDeviceFactory(NetworkServerConfiguration configuration,
                                 ILoRaDataRequestHandler dataRequestHandler,
                                 ILoRaDeviceClientConnectionManager connectionManager,
                                 ILoggerFactory loggerFactory,
                                 ILogger<LoRaDeviceFactory> logger)
        {
            this.configuration = configuration;
            this.dataRequestHandler = dataRequestHandler;
            this.connectionManager = connectionManager;
            this.loggerFactory = loggerFactory;
            this.logger = logger;
        }

        public LoRaDevice Create(IoTHubDeviceInfo deviceInfo)
        {
            if (deviceInfo is null) throw new ArgumentNullException(nameof(deviceInfo));

            var loRaDevice = new LoRaDevice(
                deviceInfo.DevAddr,
                deviceInfo.DevEUI,
                this.connectionManager)
            {
                GatewayID = deviceInfo.GatewayId,
                NwkSKey = deviceInfo.NwkSKey
            };

            var isOurDevice = string.IsNullOrEmpty(deviceInfo.GatewayId) || string.Equals(deviceInfo.GatewayId, this.configuration.GatewayID, StringComparison.OrdinalIgnoreCase);
            if (isOurDevice)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                // Ownership is transferred to connection manager.
                this.connectionManager.Register(loRaDevice, CreateDeviceClient(deviceInfo.DevEUI, deviceInfo.PrimaryKey));
#pragma warning restore CA2000 // Dispose objects before losing scope
            }

            loRaDevice.SetRequestHandler(this.dataRequestHandler);

            return loRaDevice;
        }

        private string CreateIoTHubConnectionString(string devEUI)
        {
            using var scope = this.logger.BeginDeviceScope(devEUI);

            var connectionString = string.Empty;

            if (string.IsNullOrEmpty(this.configuration.IoTHubHostName))
            {
                this.logger.LogError("Configuration/Environment variable IOTEDGE_IOTHUBHOSTNAME not found, creation of iothub connection not possible");
            }

            connectionString += $"HostName={this.configuration.IoTHubHostName};";

            if (this.configuration.EnableGateway)
            {
                connectionString += $"GatewayHostName={this.configuration.GatewayHostName};";
                this.logger.LogDebug($"using edgeHub local queue");
            }
            else
            {
                this.logger.LogDebug("using iotHub directly, no edgeHub queue");
            }

            return connectionString;
        }

        public ILoRaDeviceClient CreateDeviceClient(string eui, string primaryKey)
        {
            try
            {
                var partConnection = CreateIoTHubConnectionString(eui);
                var deviceConnectionStr = $"{partConnection}DeviceId={eui};SharedAccessKey={primaryKey}";

                // Enabling AMQP multiplexing
                var transportSettings = new ITransportSettings[]
                {
                    new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                    {
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                        {
                            Pooling = true,
                            // pool size 1 => 995 devices.
                            MaxPoolSize = 1
                        }
                    }
                };

                return new LoRaDeviceClient(eui, deviceConnectionStr, transportSettings, this.loggerFactory.CreateLogger<LoRaDeviceClient>());
            }
            catch (Exception ex)
            {
                throw new LoRaProcessingException("Could not create IoT Hub device client.", ex, LoRaProcessingErrorCode.DeviceClientCreationFailed);
            }
        }
    }
}
