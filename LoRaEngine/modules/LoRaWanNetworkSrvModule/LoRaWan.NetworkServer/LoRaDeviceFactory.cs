// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;

    public class LoRaDeviceFactory : ILoRaDeviceFactory
    {
        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDataRequestHandler dataRequestHandler;
        private readonly ILoRaDeviceClientConnectionManager connectionManager;
        private readonly LoRaDeviceCache loRaDeviceCache;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<LoRaDeviceFactory> logger;

        public LoRaDeviceFactory(NetworkServerConfiguration configuration,
                                 ILoRaDataRequestHandler dataRequestHandler,
                                 ILoRaDeviceClientConnectionManager connectionManager,
                                 LoRaDeviceCache loRaDeviceCache,
                                 ILoggerFactory loggerFactory,
                                 ILogger<LoRaDeviceFactory> logger)
        {
            this.configuration = configuration;
            this.dataRequestHandler = dataRequestHandler;
            this.connectionManager = connectionManager;
            this.loggerFactory = loggerFactory;
            this.logger = logger;
            this.loRaDeviceCache = loRaDeviceCache;
        }

        public Task<LoRaDevice> CreateAndRegisterAsync(IoTHubDeviceInfo deviceInfo, CancellationToken cancellationToken)
        {
            _ = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));

            if (string.IsNullOrEmpty(deviceInfo.PrimaryKey) || string.IsNullOrEmpty(deviceInfo.DevEUI))
                throw new ArgumentException($"Incomplete {nameof(IoTHubDeviceInfo)}", nameof(deviceInfo));

            if (this.loRaDeviceCache.TryGetByDevEui(deviceInfo.DevEUI, out _))
                throw new InvalidOperationException($"Device {deviceInfo.DevEUI} already registered");

            return RegisterCoreAsync(deviceInfo, cancellationToken);
        }

        private async Task<LoRaDevice> RegisterCoreAsync(IoTHubDeviceInfo deviceInfo, CancellationToken cancellationToken)
        {
            var loRaDevice = CreateDevice(deviceInfo);
            try
            {
                // we always want to register the connection if we have a key.
                // the connection is not opened, unless there is a
                // request made. This allows us to refresh the twins,
                // even though, we don't own it, to detect ownership
                // changes.
                // Ownership is transferred to connection manager.
                this.connectionManager.Register(loRaDevice, CreateDeviceClient(deviceInfo.DevEUI, deviceInfo.PrimaryKey));

                loRaDevice.SetRequestHandler(this.dataRequestHandler);

                if (loRaDevice.UpdateIsOurDevice(this.configuration.GatewayID))
                {
                    if (!await loRaDevice.InitializeAsync(this.configuration, cancellationToken))
                    {
                        throw new LoRaProcessingException("Failed to initialize device twins.", LoRaProcessingErrorCode.DeviceInitializationFailed);
                    }
                }
                this.loRaDeviceCache.Register(loRaDevice);
                return loRaDevice;
            }
            catch
            {
                this.connectionManager.Release(loRaDevice);
                loRaDevice.Dispose();
                throw;
            }
        }

        protected virtual LoRaDevice CreateDevice(IoTHubDeviceInfo deviceInfo)
        {
            return deviceInfo == null
                    ? throw new ArgumentNullException(nameof(deviceInfo))
                    : new LoRaDevice(deviceInfo.DevAddr,
                                     deviceInfo.DevEUI,
                                     this.connectionManager,
                                     this.loggerFactory.CreateLogger<LoRaDevice>())
                    {
                        GatewayID = deviceInfo.GatewayId,
                        NwkSKey = deviceInfo.NwkSKey,
                    };
        }

        private string CreateIoTHubConnectionString()
        {
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

        public virtual ILoRaDeviceClient CreateDeviceClient(string eui, string primaryKey)
        {
            try
            {
                var partConnection = CreateIoTHubConnectionString();
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

                return new LoRaDeviceClient(eui, deviceConnectionStr, transportSettings, primaryKey, this.loggerFactory.CreateLogger<LoRaDeviceClient>());
            }
            catch (Exception ex)
            {
                throw new LoRaProcessingException("Could not create IoT Hub device client.", ex, LoRaProcessingErrorCode.DeviceClientCreationFailed);
            }
        }
    }
}
