// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;

    // Defines the logger configuration
    public class LoggerConfiguration
    {
        // Gets/sets if logging to console is enabled
        // Default: true
        public bool LogToConsole { get; set; } = true;

        // Gets/sets the logging level
        // Default: 4 (Error)
        public LogLevel LogLevel { get; set; } = LogLevel.Error;

        // Gets/sets if logging to udp is enabled (used for integration tests mainly)
        // Default: false
        public bool LogToUdp { get; set; }

        // Gets/sets udp address to send log
        public string LogToUdpAddress { get; set; }

        // Gets/sets udp port to send logs
        public int LogToUdpPort { get; set; } = 6000;

        /// <summary>
        /// Gets or sets the id of the gateway running the logger.
        /// </summary>
        public string GatewayId { get; set; }

        public static LogLevel InitLogLevel(string logLevelIn)
        {
            LogLevel logLevelOut;

            if (logLevelIn == "1" || string.Equals(logLevelIn, "debug", StringComparison.OrdinalIgnoreCase))
            {
                logLevelOut = LogLevel.Debug;
            }
            else if (logLevelIn == "2" || string.Equals(logLevelIn, "information", StringComparison.OrdinalIgnoreCase)
                || string.Equals(logLevelIn, "info", StringComparison.OrdinalIgnoreCase))
            {
                logLevelOut = LogLevel.Information;
            }
            else
            {
                logLevelOut = LogLevel.Error;
            }

            return logLevelOut;
        }
    }
}
