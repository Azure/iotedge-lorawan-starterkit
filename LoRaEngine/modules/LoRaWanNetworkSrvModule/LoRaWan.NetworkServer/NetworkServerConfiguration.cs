// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;

    // Network server configuration
    public class NetworkServerConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether the server is running as an IoT Edge module.
        /// </summary>
        public bool RunningAsIoTEdgeModule { get; set; }

        /// <summary>
        /// Gets or sets the iot hub host name.
        /// </summary>
        public string IoTHubHostName { get; set; }

        /// <summary>
        /// Gets or sets the gateway host name.
        /// </summary>
        public string GatewayHostName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the gateway (edgeHub) is enabled.
        /// </summary>
        public bool EnableGateway { get; set; } = true;

        /// <summary>
        /// Gets or sets the gateway identifier.
        /// </summary>
        public string GatewayID { get; set; }

        /// <summary>
        /// Gets or sets the HTTP proxy url.
        /// </summary>
        public string HttpsProxy { get; set; }

        /// <summary>
        /// Gets or sets the 2nd receive windows datarate.
        /// </summary>
        public string Rx2DataRate { get; set; }

        /// <summary>
        /// Gets or sets the 2nd receive windows data frequency.
        /// </summary>
        public double? Rx2Frequency { get; set; }

        /// <summary>
        /// Gets or sets the IoT Edge timeout, 0 keeps default value,
        /// </summary>
        public uint IoTEdgeTimeout { get; set; }

        /// <summary>
        /// Gets or sets the Azure Facade function URL.
        /// </summary>
        public string FacadeServerUrl { get; set; }

        /// <summary>
        /// Gets or sets the Azure Facade Function auth code.
        /// </summary>
        public string FacadeAuthCode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether logging to console is enabled
        /// </summary>
        public bool LogToConsole { get; set; } = true;

        /// <summary>
        /// Gets or sets  the logging level.
        /// Default: 4 (Log level: Error)
        /// </summary>
        public string LogLevel { get; set; } = "4";

        /// <summary>
        /// Gets or sets a value indicating whether logging to IoT Hub is enabled.
        /// Default is false.
        /// </summary>
        public bool LogToHub { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether logging to udp is enabled (used for integration tests mainly).
        /// Default is false.
        /// </summary>
        public bool LogToUdp { get; set; }

        /// <summary>
        /// Gets or sets udp address to send log to.
        /// </summary>
        public string LogToUdpAddress { get; set; }

        /// <summary>
        /// Gets or sets udp port to send logs to.
        /// </summary>
        public int LogToUdpPort { get; set; } = 6000;

        /// <summary>
        /// Gets or sets the gateway netword id.
        /// </summary>
        public uint NetId { get; set; } = 1;

        /// <summary>
        /// Gets list of allowed dev addresses
        /// </summary>
        public HashSet<string> AllowedDevAddresses { get; internal set; }

        // Creates a new instance of NetworkServerConfiguration
        public NetworkServerConfiguration()
        {
        }

        // Creates a new instance of NetworkServerConfiguration by reading values from environment variables
        public static NetworkServerConfiguration CreateFromEnviromentVariables()
        {
            var config = new NetworkServerConfiguration();

            // Create case insensitive dictionary from environment variables
            var envVars = new CaseInsensitiveEnvironmentVariables(Environment.GetEnvironmentVariables());

            config.RunningAsIoTEdgeModule = !string.IsNullOrEmpty(envVars.GetEnvVar("IOTEDGE_APIVERSION", string.Empty));
            config.IoTHubHostName = envVars.GetEnvVar("IOTEDGE_IOTHUBHOSTNAME", string.Empty);
            config.GatewayHostName = envVars.GetEnvVar("IOTEDGE_GATEWAYHOSTNAME", string.Empty);
            config.EnableGateway = envVars.GetEnvVar("ENABLE_GATEWAY", config.EnableGateway);
            config.GatewayID = envVars.GetEnvVar("IOTEDGE_DEVICEID", string.Empty);
            config.HttpsProxy = envVars.GetEnvVar("HTTPS_PROXY", string.Empty);
            config.Rx2DataRate = envVars.GetEnvVar("RX2_DATR", string.Empty);
            if (config.Rx2Frequency.HasValue)
            {
                config.Rx2Frequency = envVars.GetEnvVar("RX2_FREQ", config.Rx2Frequency.Value);
            }

            config.IoTEdgeTimeout = envVars.GetEnvVar("IOTEDGE_TIMEOUT", config.IoTEdgeTimeout);
            config.FacadeServerUrl = envVars.GetEnvVar("FACADE_SERVER_URL", string.Empty);
            config.FacadeAuthCode = envVars.GetEnvVar("FACADE_AUTH_CODE", string.Empty);
            config.LogToHub = envVars.GetEnvVar("LOG_TO_HUB", config.LogToHub);
            config.LogLevel = envVars.GetEnvVar("LOG_LEVEL", config.LogLevel);
            config.LogToConsole = envVars.GetEnvVar("LOG_TO_CONSOLE", config.LogToConsole);
            config.LogToUdp = envVars.GetEnvVar("LOG_TO_UDP", config.LogToUdp);
            config.LogToUdpAddress = envVars.GetEnvVar("LOG_TO_UDP_ADDRESS", string.Empty);
            config.LogToUdpPort = envVars.GetEnvVar("LOG_TO_UDP_PORT", config.LogToUdpPort);
            config.NetId = envVars.GetEnvVar("NETID", config.NetId);
            config.AllowedDevAddresses = new HashSet<string>(envVars.GetEnvVar("AllowedDevAddresses", string.Empty).Split(";"));

            return config;
        }
    }
}