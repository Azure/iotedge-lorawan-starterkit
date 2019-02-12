// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;

    public class LoRaDeviceFactory : ILoRaDeviceFactory
    {
        private readonly NetworkServerConfiguration configuration;
        private readonly DefaultLoRaDataRequestHandler dataRequestHandler;

        public LoRaDeviceFactory(NetworkServerConfiguration configuration, DefaultLoRaDataRequestHandler dataRequestHandler)
        {
            this.configuration = configuration;
            this.dataRequestHandler = dataRequestHandler;
        }

        public LoRaDevice Create(IoTHubDeviceInfo deviceInfo)
        {
            var loraDeviceClient = this.CreateDeviceClient(deviceInfo.DevEUI, deviceInfo.PrimaryKey);

            var loRaDevice = new LoRaDevice(
                devAddr: deviceInfo.DevAddr,
                devEUI: deviceInfo.DevEUI,
                loRaDeviceClient: loraDeviceClient);

            loRaDevice.SetRequestHandler(this.dataRequestHandler);

            return loRaDevice;
        }

        private string CreateIoTHubConnectionString(string devEUI, string primaryKey)
        {
            string connectionString = string.Empty;

            if (string.IsNullOrEmpty(this.configuration.IoTHubHostName))
            {
                Logger.Log("Configuration/Environment variable IOTEDGE_IOTHUBHOSTNAME not found, creation of iothub connection not possible", LogLevel.Error);
            }

            connectionString += $"HostName={this.configuration.IoTHubHostName};";

            if (this.configuration.EnableGateway)
            {
                connectionString += $"GatewayHostName={this.configuration.GatewayHostName};";
                Logger.Log(devEUI, $"using edgeHub local queue", LogLevel.Information);
            }
            else
            {
                Logger.Log(devEUI, $"using iotHub directly, no edgeHub queue", LogLevel.Information);
            }

            return connectionString;
        }

        private LoRaDeviceClient CreateDeviceClient(string devEUI, string primaryKey)
        {
            try
            {
                string partConnection = this.CreateIoTHubConnectionString(devEUI, primaryKey);
                string deviceConnectionStr = $"{partConnection}DeviceId={devEUI};SharedAccessKey={primaryKey}";

                // Enabling AMQP multiplexing
                var transportSettings = new ITransportSettings[]
                {
                    new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                    {
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                        {
                            Pooling = true,
                            MaxPoolSize = 1
                        }
                    }
                };

                var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionStr, transportSettings);
                return new LoRaDeviceClient(devEUI, deviceClient);
            }
            catch (Exception ex)
            {
                Logger.Log(devEUI, $"could not create IoT Hub DeviceClient with error: {ex.Message}", LogLevel.Error);
                throw;
            }
        }
    }
}