// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Server.Kestrel.Https;

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
        public DataRateIndex? Rx2DataRate { get; set; }

        /// <summary>
        /// Gets or sets the 2nd receive windows data frequency.
        /// </summary>
        public Hertz? Rx2Frequency { get; set; }

        /// <summary>
        /// Gets or sets the IoT Edge timeout in milliseconds, 0 keeps default value,.
        /// </summary>
        public uint IoTEdgeTimeout { get; set; }

        /// <summary>
        /// Gets or sets the Azure Facade function URL.
        /// </summary>
        public Uri FacadeServerUrl { get; set; }

        /// <summary>
        /// Gets or sets the Azure Facade Function auth code.
        /// </summary>
        public string FacadeAuthCode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether logging to console is enabled.
        /// </summary>
        public bool LogToConsole { get; set; } = true;

        /// <summary>
        /// Gets or sets  the logging level.
        /// Default: 4 (Log level: Error).
        /// </summary>
        public string LogLevel { get; set; } = "4";

        /// <summary>
        /// Gets or sets a value indicating whether logging to TCP is enabled (used for integration tests mainly).
        /// Default is false.
        /// </summary>
        public bool LogToTcp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether logging to IoT Hub is enabled.
        /// Default is false.
        /// </summary>
        public bool LogToHub { get; set; }

        /// <summary>
        /// Gets or sets TCP address to send log to.
        /// </summary>
        public string LogToTcpAddress { get; set; }

        /// <summary>
        /// Gets or sets TCP port to send logs to.
        /// </summary>
        public int LogToTcpPort { get; set; } = 6000;

        /// <summary>
        /// Gets or sets the gateway netword id.
        /// </summary>
        public uint NetId { get; set; } = 1;

        /// <summary>
        /// Gets list of allowed dev addresses.
        /// </summary>
        public HashSet<string> AllowedDevAddresses { get; internal set; }

        /// <summary>
        /// Path of the .pfx certificate to be used for LNS Server endpoint
        /// </summary>
        public string LnsServerPfxPath { get; internal set; }

        /// <summary>
        /// Password of the .pfx certificate to be used for LNS Server endpoint
        /// </summary>
        public string LnsServerPfxPassword { get; internal set; }

        /// <summary>
        /// Specifies the client certificate mode with which the server should be run
        /// Allowed values can be found at https://docs.microsoft.com/dotnet/api/microsoft.aspnetcore.server.kestrel.https.clientcertificatemode?view=aspnetcore-6.0
        /// </summary>
        public ClientCertificateMode ClientCertificateMode { get; internal set; }

        // Creates a new instance of NetworkServerConfiguration by reading values from environment variables
        public static NetworkServerConfiguration CreateFromEnvironmentVariables()
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
            config.Rx2DataRate = envVars.GetEnvVar("RX2_DATR", -1) is var datrNum && (DataRateIndex)datrNum is var datr && Enum.IsDefined(datr) ? datr : null;
            config.Rx2Frequency = envVars.GetEnvVar("RX2_FREQ") is { } someFreq ? Hertz.Mega(someFreq) : null;
            config.IoTEdgeTimeout = envVars.GetEnvVar("IOTEDGE_TIMEOUT", config.IoTEdgeTimeout);

            // facadeurl is allowed to be null as the value is coming from the twin in production.
            var facadeUrl = envVars.GetEnvVar("FACADE_SERVER_URL", string.Empty);
            config.FacadeServerUrl = string.IsNullOrEmpty(facadeUrl) ? null : new Uri(envVars.GetEnvVar("FACADE_SERVER_URL", string.Empty));
            config.FacadeAuthCode = envVars.GetEnvVar("FACADE_AUTH_CODE", string.Empty);
            config.LogLevel = envVars.GetEnvVar("LOG_LEVEL", config.LogLevel);
            config.LogToConsole = envVars.GetEnvVar("LOG_TO_CONSOLE", config.LogToConsole);
            config.LogToTcp = envVars.GetEnvVar("LOG_TO_TCP", config.LogToTcp);
            config.LogToHub = envVars.GetEnvVar("LOG_TO_HUB", config.LogToHub);
            config.LogToTcpAddress = envVars.GetEnvVar("LOG_TO_TCP_ADDRESS", string.Empty);
            config.LogToTcpPort = envVars.GetEnvVar("LOG_TO_TCP_PORT", config.LogToTcpPort);
            config.NetId = envVars.GetEnvVar("NETID", config.NetId);
            config.AllowedDevAddresses = new HashSet<string>(envVars.GetEnvVar("AllowedDevAddresses", string.Empty).Split(";"));
            config.LnsServerPfxPath = envVars.GetEnvVar("LNS_SERVER_PFX_PATH", string.Empty);
            config.LnsServerPfxPassword = envVars.GetEnvVar("LNS_SERVER_PFX_PASSWORD", string.Empty);
            var clientCertificateModeString = envVars.GetEnvVar("CLIENT_CERTIFICATE_MODE", "NoCertificate"); // Defaulting to NoCertificate if missing mode
            config.ClientCertificateMode = Enum.Parse<ClientCertificateMode>(clientCertificateModeString, true);

            return config;
        }
    }
}
