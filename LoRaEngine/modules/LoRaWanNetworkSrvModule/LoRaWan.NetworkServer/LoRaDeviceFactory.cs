//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//


using System;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;

namespace LoRaWan.NetworkServer
{
    public class LoRaDeviceFactory : ILoRaDeviceFactory
    {
        private readonly NetworkServerConfiguration configuration;

        public LoRaDeviceFactory(NetworkServerConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public LoRaDevice Create(IoTHubDeviceInfo deviceInfo)
        {
            var loraDeviceClient = CreateDeviceClient(deviceInfo.DevEUI, deviceInfo.PrimaryKey);

            var loRaDevice = new LoRaDevice(
                devAddr: deviceInfo.DevAddr,
                devEUI: deviceInfo.DevEUI,
                loRaDeviceClient: loraDeviceClient);

            return loRaDevice;
        }

        private string CreateIoTHubConnectionString(string devEUI, string primaryKey)
        {
            string connectionString = string.Empty;


            if (string.IsNullOrEmpty(configuration.IoTHubHostName))
            {
                Logger.Log("Configuration/Environment variable IOTEDGE_IOTHUBHOSTNAME not found, creation of iothub connection not possible", Logger.LoggingLevel.Error);
            }

            connectionString += $"HostName={configuration.IoTHubHostName};";

            if (configuration.EnableGateway)
            {
                connectionString += $"GatewayHostName={configuration.GatewayHostName};";
                Logger.Log(devEUI, $"using edgeHub local queue", Logger.LoggingLevel.Info);
            }
            else
            {
                Logger.Log(devEUI, $"using iotHub directly, no edgeHub queue", Logger.LoggingLevel.Info);
            }
            return connectionString;
        }

        private LoRaDeviceClient CreateDeviceClient(string devEUI, string primaryKey)
        {
            try
            {
                string partConnection = CreateIoTHubConnectionString(devEUI, primaryKey);
                string deviceConnectionStr = $"{partConnection}DeviceId={devEUI};SharedAccessKey={primaryKey}";

                var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionStr, TransportType.Amqp_Tcp_Only);
                return new LoRaDeviceClient(devEUI, deviceClient);
            }
            catch (Exception ex)
            {
                Logger.Log(devEUI, $"could not create IoT Hub DeviceClient with error: {ex.Message}", Logger.LoggingLevel.Error);
                throw;
            }
        }
    }
}