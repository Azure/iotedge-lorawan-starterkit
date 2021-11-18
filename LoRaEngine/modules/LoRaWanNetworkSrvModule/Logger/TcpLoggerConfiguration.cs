// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using Microsoft.Extensions.Logging;

    // Defines the logger configuration
    public class TcpLoggerConfiguration
    {
        public TcpLoggerConfiguration(LogLevel logLevel, string logToTcpAddress, int logToTcpPort, string gatewayId)
        {
            LogLevel = logLevel;
            LogToTcpAddress = logToTcpAddress;
            LogToTcpPort = logToTcpPort;
            GatewayId = gatewayId;
        }

        // Gets the logging level
        public LogLevel LogLevel { get; }

        // Gets TCP address to send log
        public string LogToTcpAddress { get; }

        // Gets/sets TCP port to send logs
        public int LogToTcpPort { get; }

        /// <summary>
        /// Gets or sets the id of the gateway running the logger.
        /// </summary>
        public string GatewayId { get; }

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
