// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Configuration;

    public static class LoRaRegistryManager
    {
        private static RegistryManager current;
        private static object singletonLock = new object();

        public static RegistryManager GetCurrentInstance(string functionAppDirectory)
        {
            if (current != null)
            {
                return current;
            }

            lock (singletonLock)
            {
                if (current == null)
                {
                    var config = new ConfigurationBuilder()
                    .SetBasePath(functionAppDirectory)
                    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();
                    var connectionString = config.GetConnectionString("IoTHubConnectionString");
                    if (connectionString == null)
                    {
                        string errorMsg = "Missing IoTHubConnectionString in settings";
                        throw new Exception(errorMsg);
                    }

                    current = RegistryManager.CreateFromConnectionString(connectionString);
                }
            }

            return current;
        }
    }
}
