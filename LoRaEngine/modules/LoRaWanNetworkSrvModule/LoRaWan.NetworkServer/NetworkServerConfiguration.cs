//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace LoRaWan.NetworkServer
{
    // Network server configuration
    public class NetworkServerConfiguration
    {
        // Gets/sets if the server is running as an IoT Edge module
        public bool RunningAsIoTEdgeModule { get; set; }

        // Gets/sets the iot hub host name
        public string IoTHubHostName { get; set; }

        // Gets/sets the gateway host name
        public string GatewayHostName { get; set; }

        // Gets/sets if the gateway (edgeHub) is enabled     
        public bool EnableGateway { get; set; } = true;

        // Gets/sets the gateway identifier
        public string GatewayID { get; set; }

        // Gets/sets the Http proxy URL
        public string HttpsProxy { get; set; }

        // Gets/sets the 2nd receive windows data rate
        // TODO: better documentation and property name
        public string Rx2DataRate { get; set; }

        // Gets/sets the 2nd receive windows data frequency
        public double Rx2DataFrequency { get; set; }

        // Gets/sets a IoT edge timeout, 0 keeps the default value
        public uint IoTEdgeTimeout { get; set; }

        // Gets/sets the Azure Facade Function URL
        public string FacadeServerUrl { get; set; }

        // Gets/sets the Azure Facade Function auth code
        public string FacadeAuthCode { get; set; }

        // Gets/sets if logging to console is enabled
        // Default: true
        public bool LogToConsole { get;  set; } = true;

        // Gets/sets the logging level
        // Default: 0 (Always logging)
        public int LogLevel { get;  set; } = 0;

        // Gets/sets if logging to IoT Hub is enabled
        // Default: false
        public bool LogToHub { get;  set; }

        // Gets/sets if logging to udp is enabled (used for integration tests mainly)
        // Default: false
        public bool LogToUdp { get; set; }

        // Gets/sets udp address to send log
        public string LogToUdpAddress { get; set; }

        // Gets/sets udp port to send logs
        public int LogToUdpPort { get; set; } = 6000;

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
            config.Rx2DataFrequency = envVars.GetEnvVar("RX2_FREQ", config.Rx2DataFrequency);
            config.IoTEdgeTimeout = envVars.GetEnvVar("IOTEDGE_TIMEOUT", config.IoTEdgeTimeout);
            config.FacadeServerUrl = envVars.GetEnvVar("FacadeServerUrl", string.Empty);
            config.FacadeAuthCode = envVars.GetEnvVar("FacadeAuthCode", string.Empty);
            config.LogToHub = envVars.GetEnvVar("LOG_TO_HUB", config.LogToHub);
            config.LogLevel = envVars.GetEnvVar("LOG_LEVEL", config.LogLevel);
            config.LogToConsole = envVars.GetEnvVar("LOG_TO_CONSOLE", config.LogToConsole);
            config.LogToUdp = envVars.GetEnvVar("LOG_TO_UDP", config.LogToUdp);
            config.LogToUdpAddress = envVars.GetEnvVar("LOG_TO_UDP_ADDRESS", string.Empty);
            config.LogToUdpPort = envVars.GetEnvVar("LOG_TO_UDP_PORT", config.LogToUdpPort);

            return config;
        }
    }
}