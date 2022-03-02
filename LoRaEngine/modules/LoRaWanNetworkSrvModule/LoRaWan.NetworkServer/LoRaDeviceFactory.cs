// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Diagnostics.Metrics;
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
        private readonly Meter meter;
        private readonly ITracing tracing;

        public LoRaDeviceFactory(NetworkServerConfiguration configuration,
                                 ILoRaDataRequestHandler dataRequestHandler,
                                 ILoRaDeviceClientConnectionManager connectionManager,
                                 LoRaDeviceCache loRaDeviceCache,
                                 ILoggerFactory loggerFactory,
                                 ILogger<LoRaDeviceFactory> logger,
                                 Meter meter,
                                 ITracing tracing)
        {
            this.configuration = configuration;
            this.dataRequestHandler = dataRequestHandler;
            this.connectionManager = connectionManager;
            this.loggerFactory = loggerFactory;
            this.logger = logger;
            this.meter = meter;
            this.tracing = tracing;
            this.loRaDeviceCache = loRaDeviceCache;
        }

        public Task<LoRaDevice> CreateAndRegisterAsync(IoTHubDeviceInfo deviceInfo, CancellationToken cancellationToken)
        {
            _ = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));

            if (string.IsNullOrEmpty(deviceInfo.PrimaryKey) || !deviceInfo.DevEUI.IsValid)
                throw new ArgumentException($"Incomplete {nameof(IoTHubDeviceInfo)}", nameof(deviceInfo));

            if (this.loRaDeviceCache.TryGetByDevEui(deviceInfo.DevEUI, out _))
                throw new InvalidOperationException($"Device {deviceInfo.DevEUI} already registered");

            return RegisterCoreAsync(deviceInfo, cancellationToken);
        }

        private async Task<LoRaDevice> RegisterCoreAsync(IoTHubDeviceInfo deviceInfo, CancellationToken cancellationToken)
        {
            var loRaDevice = CreateDevice(deviceInfo);
            var loRaDeviceClient = CreateDeviceClient(deviceInfo.DevEUI.ToString(), deviceInfo.PrimaryKey);
            try
            {
                // we always want to register the connection if we have a key.
                // the connection is not opened, unless there is a
                // request made. This allows us to refresh the twins,
                // even though, we don't own it, to detect ownership
                // changes.
                // Ownership is transferred to connection manager.
                this.connectionManager.Register(loRaDevice, loRaDeviceClient);

                loRaDevice.SetRequestHandler(this.dataRequestHandler);

                if (loRaDevice.UpdateIsOurDevice(this.configuration.GatewayID) &&
                    !await loRaDevice.InitializeAsync(this.configuration, cancellationToken))
                {
                    throw new LoRaProcessingException("Failed to initialize device twins.", LoRaProcessingErrorCode.DeviceInitializationFailed);
                }

                this.loRaDeviceCache.Register(loRaDevice);

                return loRaDevice;
            }
            catch
            {
                // release the loradevice client explicitly. If we were unable to register, or there was already
                // a connection registered, we will leak this client.
                await loRaDeviceClient.DisposeAsync();

                if (this.logger.IsEnabled(LogLevel.Debug))
                {
                    try
                    {
                        // if the created client is registered, release it
                        if (!ReferenceEquals(loRaDeviceClient, ((IIdentityProvider<ILoRaDeviceClient>)this.connectionManager.GetClient(loRaDevice)).Identity))
                        {
                            this.logger.LogDebug("leaked connection found");
                        }
                    }
                    catch (ManagedConnectionException) { }
                }

                await loRaDevice.DisposeAsync();
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
                                     this.loggerFactory.CreateLogger<LoRaDevice>(),
                                     this.meter)
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

        public virtual ILoRaDeviceClient CreateDeviceClient(string deviceId, string primaryKey)
        {
            try
            {
                var partConnection = CreateIoTHubConnectionString();
                var deviceConnectionStr = FormattableString.Invariant($"{partConnection}DeviceId={deviceId};SharedAccessKey={primaryKey}");

                // Enabling AMQP multiplexing
                var transportSettings = new ITransportSettings[]
                {
                    new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                    {
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                        {
                            Pooling = true,
                            // pool size for this project defaults to 1, allowing aroung 1000 amqp links
                            // in case you need more, and the communications are proxied through edgeHub,
                            // please consider changing this parameter in edgeHub configuration too
                            // https://github.com/Azure/iotedge/blob/e6d52d6f6b0eb76e7ef250f3fcdeaf38e467ab4f/doc/EnvironmentVariables.md
                            MaxPoolSize = this.configuration.IotHubConnectionPoolSize
                        },
                        OperationTimeout = TimeSpan.FromSeconds(10)
                    }
                };

                var client = new LoRaDeviceClient(deviceId, deviceConnectionStr, transportSettings,
                                                  this.loggerFactory.CreateLogger<LoRaDeviceClient>(), this.meter,
                                                  this.tracing);

                return client.AddResiliency(this.loggerFactory);
            }
            catch (Exception ex)
            {
                throw new LoRaProcessingException("Could not create IoT Hub device client.", ex, LoRaProcessingErrorCode.DeviceClientCreationFailed);
            }
        }
    }
}
