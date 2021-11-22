// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using Microsoft.Extensions.Logging;

    // Defines the logger configuration
    public sealed class TcpLoggerConfiguration
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
    }
}
