// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using Microsoft.Extensions.Configuration;

    public static class FunctionConfigManager
    {
        private static readonly object SingletonLock = new object();

        private static IConfigurationRoot config;

        public static IConfigurationRoot GetCurrentConfiguration(string functionAppDirectory)
        {
            if (config != null)
            {
                return config;
            }

            lock (SingletonLock)
            {
                if (config == null)
                {
                    config = new ConfigurationBuilder()
                    .SetBasePath(functionAppDirectory)
                    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .Build();
                }
            }

            return config;
        }
    }
}
