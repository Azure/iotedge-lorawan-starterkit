// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cli_LoRa_Device_Checker
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Configuration;

    public class ConfigurationHelper
    {
        public string ConnectionString { get; set; }

        public RegistryManager RegistryManager { get; set; }

        public bool ReadConfig()
        {
            var connectionString = string.Empty;

            try
            {
                // Get configuration
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("settings.json", optional: false, reloadOnChange: true)
                    .Build();

                connectionString = config["IoTHubConnectionString"];
            }
            catch (Exception ex)
            {
                StatusConsole.WriteLine(MessageType.Error, $"{ex.Message}");
                Console.WriteLine("The format should be: { \"IoTHubConnectionString\" : \"HostName=xxx.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=xxx\" }");
                return false;
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Using IoT Hub connection string: {connectionString}");
            Console.ResetColor();

            try
            {
                this.RegistryManager = RegistryManager.CreateFromConnectionString(connectionString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to IoT Hub (possible error in connection string): {ex.Message}");
                return false;
            }

            return true;
        }
    }
}
