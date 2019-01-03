//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//


using System;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;

namespace LoRaWan.NetworkServer.V2
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

        public async Task InitializeAsync(LoRaDevice loraDevice)
        {
            var twin = await loraDevice.GetTwinAsync();

            if (twin != null)
            {
                //ABP Case
                if (twin.Properties.Desired.Contains(TwinProperty.AppSKey))
                {
                    loraDevice.AppSKey = twin.Properties.Desired[TwinProperty.AppSKey];
                    loraDevice.NwkSKey = twin.Properties.Desired[TwinProperty.NwkSKey];
                    loraDevice.DevAddr = twin.Properties.Desired[TwinProperty.DevAddr];
                    loraDevice.IsABP = true;
                }
                //OTAA Case
                else if (twin.Properties.Reported.Contains(TwinProperty.AppSKey))
                {
                    loraDevice.AppSKey = twin.Properties.Reported[TwinProperty.AppSKey];
                    loraDevice.NwkSKey = twin.Properties.Reported[TwinProperty.NwkSKey];
                    loraDevice.DevAddr = twin.Properties.Reported[TwinProperty.DevAddr];
                    loraDevice.DevNonce = twin.Properties.Reported[TwinProperty.DevNonce];

                    //todo check if appkey and appeui is needed in the flow
                    loraDevice.AppEUI = twin.Properties.Desired[TwinProperty.AppEUI];
                    loraDevice.AppKey = twin.Properties.Desired[TwinProperty.AppKey];
                }
                else
                {
                    Logger.Log(loraDevice.DevEUI, $"AppSKey not present neither in Desired or in Reported properties", Logger.LoggingLevel.Error);
                }

                if (twin.Properties.Desired.Contains(TwinProperty.GatewayID))
                    loraDevice.GatewayID = twin.Properties.Desired[TwinProperty.GatewayID];
                if (twin.Properties.Desired.Contains(TwinProperty.SensorDecoder))
                    loraDevice.SensorDecoder = twin.Properties.Desired[TwinProperty.SensorDecoder];
                loraDevice.IsOurDevice = true;
                if (twin.Properties.Reported.Contains(TwinProperty.FCntUp))
                    loraDevice.SetFcntUp((int)twin.Properties.Reported[TwinProperty.FCntUp]);
                if (twin.Properties.Reported.Contains(TwinProperty.FCntDown))
                    loraDevice.SetFcntDown((int)twin.Properties.Reported[TwinProperty.FCntDown]);

                Logger.Log(loraDevice.DevEUI, $"done getting twins", Logger.LoggingLevel.Info);

            }
        }
    }
}