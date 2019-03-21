// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cli_LoRa_Device_Provisioning
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Configuration;

    public class ConfigurationHelper
    {
        public string NetId { get; set; }

        public RegistryManager RegistryManager { get; set; }

        public bool ReadConfig()
        {
            var connectionString = string.Empty;
            var netId = string.Empty;

            Console.WriteLine("Reading configuration file \"settings.json\".");

            // Read configuration file settings.json
            try
            {
                var configurationBuilder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("settings.json", optional: false, reloadOnChange: true)
                    .Build();

                connectionString = configurationBuilder["IoTHubConnectionString"];
                netId = configurationBuilder["NetId"];
            }
            catch (Exception ex)
            {
                StatusConsole.WriteLine(MessageType.Error, $"{ex.Message}");
                return false;
            }

            // Validate connection setting
            if (string.IsNullOrEmpty(connectionString))
            {
                StatusConsole.WriteLine(MessageType.Error, "Connection string not found in settings file. The format should be: { \"IoTHubConnectionString\" : \"HostName=xxx.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=xxx\" }.");
                return false;
            }
            else
            {
                StatusConsole.WriteLine(MessageType.Info, $"Using IoT Hub connection string: {connectionString}.");
            }

            // Validate NetId setting
            if (string.IsNullOrEmpty(netId))
            {
                netId = ValidationHelper.CleanNetId(Constants.DefaultNetId.ToString());
                StatusConsole.WriteLine(MessageType.Info, $"NetId is not set in settings file. Using default {netId}.");
            }
            else
            {
                netId = ValidationHelper.CleanNetId(netId);

                if (ValidationHelper.ValidateHexStringTwinProperty(netId, 3, out string customError))
                {
                    StatusConsole.WriteLine(MessageType.Info, $"Using NetId {netId} from settings file.");
                }
                else
                {
                    var netIdBad = netId;
                    netId = ValidationHelper.CleanNetId(Constants.DefaultNetId.ToString());
                    StatusConsole.WriteLine(MessageType.Warning, $"NetId {netIdBad} in settings file is invalid. {customError}.");
                    StatusConsole.WriteLine(MessageType.Warning, $"Using default NetId {netId} instead.");
                }
            }

            StatusConsole.WriteLine(MessageType.Info, $"To override, use --netid parameter.");

            this.NetId = netId;

            // Create Registry Manager using connection string
            try
            {
                this.RegistryManager = RegistryManager.CreateFromConnectionString(connectionString);
            }
            catch (Exception ex)
            {
                StatusConsole.WriteLine(MessageType.Error, $"Can not connect to IoT Hub (possible error in connection string): {ex.Message}.");
                return false;
            }

            return true;
        }
    }
}
