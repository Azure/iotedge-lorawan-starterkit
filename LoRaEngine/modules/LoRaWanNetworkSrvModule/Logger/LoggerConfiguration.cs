// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using Microsoft.Azure.Devices.Client;

    // Defines the logger configuration
    public class LoggerConfiguration
    {
        // Gets/sets the module client
        public ModuleClient ModuleClient { get; set; }

        // Gets/sets if logging to console is enabled
        // Default: true
        public bool LogToConsole { get; set; } = true;

        // Gets/sets the logging level
        // Default: 4 (Error)
        public int LogLevel { get; set; } = 4;

        // Gets/sets if logging to IoT Hub is enabled
        // Default: false
        public bool LogToHub { get; set; }

        // Gets/sets if logging to udp is enabled (used for integration tests mainly)
        // Default: false
        public bool LogToUdp { get; set; }

        // Gets/sets udp address to send log
        public string LogToUdpAddress { get; set; }

        // Gets/sets udp port to send logs
        public int LogToUdpPort { get; set; } = 6000;

        public static int InitLogLevel(string logLevel)
        {
            int logLevelInt = 4;
            logLevel = logLevel.ToUpper();

            if (logLevel == "1" || logLevel == "DEBUG")
            {
                logLevelInt = 1;
            }
            else if (logLevel == "2" || logLevel == "INFORMATION" || logLevel == "INFO")
            {
                logLevelInt = 2;
            }
            else if (logLevel == "3" || logLevel == "4" || logLevel == "ERROR")
            {
                logLevelInt = 4;
            }

            return logLevelInt;
        }
    }
}