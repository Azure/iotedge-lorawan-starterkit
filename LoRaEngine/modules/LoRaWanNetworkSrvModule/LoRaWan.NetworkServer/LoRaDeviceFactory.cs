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

        public LoRaDeviceFactory(NetworkServerConfiguration configuration,
                                 ILoRaDataRequestHandler dataRequestHandler,
                                 ILoRaDeviceClientConnectionManager connectionManager,
                                 LoRaDeviceCache loRaDeviceCache)
        {
            this.configuration = configuration;
            this.dataRequestHandler = dataRequestHandler;
            this.connectionManager = connectionManager;
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
#pragma warning disable CA2000 // Dispose objects before losing scope, ownership is transferred to the cache
            var loRaDevice = new LoRaDevice(
                deviceInfo.DevAddr,
                deviceInfo.DevEUI,
                this.connectionManager)
            {
                GatewayID = deviceInfo.GatewayId,
                NwkSKey = deviceInfo.NwkSKey,
            };
            try
            {
                // we always want to register the connection if we have a key.
                // the connection is not opened, unless there is a
                // request made. This allows us to refresh the twins,
                // even though, we don't own it, to detect ownership
                // changes.
                // Ownership is transferred to connection manager.
                this.connectionManager.Register(loRaDevice, CreateDeviceClient(deviceInfo.DevEUI, deviceInfo.PrimaryKey));
#pragma warning restore CA2000 // Dispose objects before losing scope

                loRaDevice.SetRequestHandler(this.dataRequestHandler);

                if (loRaDevice.UpdateIsOurDevice(this.configuration.GatewayID))
                {
                    if (!await loRaDevice.InitializeAsync(this.configuration, cancellationToken))
                    {
                        Logger.Log(loRaDevice.DevEUI, "Failed to initialize device twins. Releaseing resources and not caching the device", LogLevel.Error);

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

        private string CreateIoTHubConnectionString(string devEUI)
        {
            var connectionString = string.Empty;

            if (string.IsNullOrEmpty(this.configuration.IoTHubHostName))
            {
                Logger.Log("Configuration/Environment variable IOTEDGE_IOTHUBHOSTNAME not found, creation of iothub connection not possible", LogLevel.Error);
            }

            connectionString += $"HostName={this.configuration.IoTHubHostName};";

            if (this.configuration.EnableGateway)
            {
                connectionString += $"GatewayHostName={this.configuration.GatewayHostName};";
                Logger.Log(devEUI, $"using edgeHub local queue", LogLevel.Debug);
            }
            else
            {
                Logger.Log(devEUI, $"using iotHub directly, no edgeHub queue", LogLevel.Debug);
            }

            return connectionString;
        }

        public virtual ILoRaDeviceClient CreateDeviceClient(string eui, string primaryKey)
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

                return new LoRaDeviceClient(eui, deviceConnectionStr, transportSettings, primaryKey);
            }
            catch (Exception ex)
            {
                throw new LoRaProcessingException("Could not create IoT Hub device client.", ex, LoRaProcessingErrorCode.DeviceClientCreationFailed);
            }
        }
    }
}
