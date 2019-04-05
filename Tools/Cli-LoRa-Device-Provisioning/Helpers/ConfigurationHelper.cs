// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Helpers
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
            string connectionString, netId;

            Console.WriteLine("Reading configuration file \"appsettings.json\"...");

            // Read configuration file appsettings.json
            try
            {
                var configurationBuilder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
                    .Build();

                connectionString = configurationBuilder["IoTHubConnectionString"];
                netId = configurationBuilder["NetId"];
            }
            catch (Exception ex)
            {
                StatusConsole.WriteLogLine(MessageType.Error, $"{ex.Message}");
                StatusConsole.WriteLogLine(MessageType.Info, "The file should have the following structure: { \"IoTHubConnectionString\" : \"HostName=xxx.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=xxx\" }");
                return false;
            }

            // Validate connection setting
            if (string.IsNullOrEmpty(connectionString))
            {
                StatusConsole.WriteLogLine(MessageType.Error, "Connection string not found in settings file. The format should be: { \"IoTHubConnectionString\" : \"HostName=xxx.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=xxx\" }.");
                return false;
            }
            else
            {
                // Just show IoT Hub Hostname
                if (this.GetHostFromConnectionString(connectionString, out string hostName))
                {
                    StatusConsole.WriteLogLine(MessageType.Info, $"Using IoT Hub: {hostName}");
                }
                else
                {
                    StatusConsole.WriteLogLine(MessageType.Error, "Invalid connection string in appsettings.json. Can not find \"HostName=\". The file should have the following structure: { \"IoTHubConnectionString\" : \"HostName=xxx.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=xxx\" }.");
                    return false;
                }
            }

            // Validate NetId setting
            if (string.IsNullOrEmpty(netId))
            {
                netId = ValidationHelper.CleanNetId(Constants.DefaultNetId.ToString());
                StatusConsole.WriteLogLine(MessageType.Info, $"NetId is not set in settings file. Using default {netId}.");
            }
            else
            {
                netId = ValidationHelper.CleanNetId(netId);

                if (ValidationHelper.ValidateHexStringTwinProperty(netId, 3, out string customError))
                {
                    StatusConsole.WriteLogLine(MessageType.Info, $"Using NetId {netId} from settings file.");
                }
                else
                {
                    var netIdBad = netId;
                    netId = ValidationHelper.CleanNetId(Constants.DefaultNetId.ToString());
                    StatusConsole.WriteLogLine(MessageType.Warning, $"NetId {netIdBad} in settings file is invalid. {customError}.");
                    StatusConsole.WriteLogLine(MessageType.Warning, $"Using default NetId {netId} instead.");
                }
            }

            StatusConsole.WriteLogLine(MessageType.Info, $"To override, use --netid parameter.");

            this.NetId = netId;

            // Create Registry Manager using connection string
            try
            {
                this.RegistryManager = RegistryManager.CreateFromConnectionString(connectionString);
            }
            catch (Exception ex)
            {
                StatusConsole.WriteLogLine(MessageType.Error, $"Can not connect to IoT Hub (possible error in connection string): {ex.Message}.");
                return false;
            }

            Console.WriteLine("done.");
            return true;
        }

        public bool GetHostFromConnectionString(string connectionString, out string hostName)
        {
            hostName = string.Empty;

            var from = connectionString.IndexOf("HostName=");
            var fromOffset = "HostName=".Length;

            var to = connectionString.IndexOf("azure-devices.net");
            var length = to - from - fromOffset + "azure-devices.net".Length;

            if (from == -1 || to == -1)
            {
                return false;
            }
            else
            {
                hostName = connectionString.Substring(from + fromOffset, length);
                return true;
            }
        }
    }
}
