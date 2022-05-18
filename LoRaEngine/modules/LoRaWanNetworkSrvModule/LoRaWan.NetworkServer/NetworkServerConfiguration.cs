// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Configuration;

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
        public int LogToTcpPort { get; set; }

        /// <summary>
        /// Gets or sets the gateway netword id.
        /// </summary>
        public NetId NetId { get; set; }

        /// <summary>
        /// Gets list of allowed dev addresses.
        /// </summary>
        public HashSet<DevAddr> AllowedDevAddresses { get; internal set; }

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

        /// <summary>
        /// Gets the version of the LNS.
        /// </summary>
        public string LnsVersion { get; private set; }

        /// <summary>
        /// Gets the connection string of Redis server for Pub/Sub functionality in Cloud only deployments.
        /// </summary>
        public string RedisConnectionString { get; private set; }

        /// Specifies the pool size for upstream AMQP connection
        /// </summary>
        public uint IotHubConnectionPoolSize { get; internal set; }

        /// <summary>
        /// Specifies the Processing Delay in Milliseconds
        /// </summary>
        public int ProcessingDelayInMilliseconds { get; set; }

        // Creates a new instance of NetworkServerConfiguration by reading values from environment variables
        public static NetworkServerConfiguration Create(IConfiguration configuration)
        {
            var networkServerConfiguration = new NetworkServerConfiguration();

            void SetPropertyIfDefined(Action<NetworkServerConfiguration, string> assign, params string[] configurationValueNames)
            {
                foreach (var configurationValueName in configurationValueNames)
                {
                    var value = configuration.GetValue<string>(configurationValueName);

                    if (!string.IsNullOrEmpty(value))
                    {
                        assign(networkServerConfiguration, value);
                        break;
                    }
                }
            }

            void SetGenericIfDefined<T, U>(string configurationValueName,
                                           Action<NetworkServerConfiguration, U> assign,
                                           T defaultValue,
                                           Func<T, U> selector) =>
                assign(networkServerConfiguration, selector(configuration.GetValue<T>(configurationValueName, defaultValue)));

            void SetGenericIfDefinedNoSelector<T>(string configurationValueName,
                                                  Action<NetworkServerConfiguration, T> assign,
                                                  T defaultValue) =>
                SetGenericIfDefined(configurationValueName, assign, defaultValue, v => v);

            // Create case insensitive dictionary from environment variables
            SetGenericIfDefinedNoSelector("PROCESSING_DELAY_IN_MS", (c, v) => c.ProcessingDelayInMilliseconds = v, Constants.DefaultProcessingDelayInMilliseconds);
            SetGenericIfDefined("CLOUD_DEPLOYMENT", (c, v) => c.RunningAsIoTEdgeModule = v, false, v => !v);

            SetPropertyIfDefined((c, v) => c.IoTHubHostName = v, "IOTEDGE_IOTHUBHOSTNAME", "IOTHUBHOSTNAME");
            if (string.IsNullOrEmpty(networkServerConfiguration.IoTHubHostName))
                throw new InvalidOperationException("Either 'IOTEDGE_IOTHUBHOSTNAME' or 'IOTHUBHOSTNAME' environment variable should be populated");

            SetPropertyIfDefined((c, v) => c.GatewayHostName = v, "IOTEDGE_GATEWAYHOSTNAME");
            SetGenericIfDefinedNoSelector("ENABLE_GATEWAY", (c, v) => c.EnableGateway = v, true);
            SetGenericIfDefinedNoSelector("ENABLE_GATEWAY", (c, v) => c.EnableGateway = v, true);
            if (!networkServerConfiguration.RunningAsIoTEdgeModule && networkServerConfiguration.EnableGateway)
                throw new NotSupportedException("ENABLE_GATEWAY cannot be true if RunningAsIoTEdgeModule is false.");

            SetPropertyIfDefined((c, v) => c.GatewayID = v, "IOTEDGE_DEVICEID", "HOSTNAME");
            if (string.IsNullOrEmpty(networkServerConfiguration.GatewayID))
                throw new InvalidOperationException("Either 'IOTEDGE_DEVICEID' or 'HOSTNAME' environment variable should be populated");

            SetPropertyIfDefined((c, v) => c.HttpsProxy = v, "HTTPS_PROXY");
            SetGenericIfDefined<int, DataRateIndex?>("RX2_DATR", (c, v) => c.Rx2DataRate = v, -1, v => v is var datrNum && (DataRateIndex)datrNum is var datr && Enum.IsDefined(datr) ? datr : null);
            SetGenericIfDefined<double?, Hertz?>("RX2_FREQ", (c, v) => c.Rx2Frequency = v, null, v => v is { } someFreq ? Hertz.Mega(someFreq) : null);

            SetGenericIfDefinedNoSelector("IOTEDGE_TIMEOUT", (c, v) => c.IoTEdgeTimeout = v, networkServerConfiguration.IoTEdgeTimeout);

            // facadeurl is allowed to be null as the value is coming from the twin in production.
            SetGenericIfDefined<string, Uri>("FACADE_SERVER_URL", (c, v) => c.FacadeServerUrl = v, null, v => string.IsNullOrEmpty(v) ? null : new Uri(v));
            SetPropertyIfDefined((c, v) => c.FacadeAuthCode = v, "FACADE_AUTH_CODE");
            SetPropertyIfDefined((c, v) => c.LogLevel = v, "LOG_LEVEL");
            SetGenericIfDefinedNoSelector("LOG_TO_CONSOLE", (c, v) => c.LogToConsole = v, false);
            SetGenericIfDefinedNoSelector("LOG_TO_TCP", (c, v) => c.LogToTcp = v, false);
            SetGenericIfDefinedNoSelector("LOG_TO_HUB", (c, v) => c.LogToHub = v, false);
            SetPropertyIfDefined((c, v) => c.LogToTcpAddress = v, "LOG_TO_TCP_ADDRESS");
            SetGenericIfDefinedNoSelector("LOG_TO_TCP_PORT", (c, v) => c.LogToTcpPort = v, 6000);
            SetGenericIfDefined("NETID", (c, v) => c.NetId = v, 1, v => new NetId(v));
            SetGenericIfDefined("AllowedDevAddresses", (c, v) => c.AllowedDevAddresses = v, string.Empty, v => v.Split(";")
                .Select(s => DevAddr.TryParse(s, out var devAddr) ? (true, Value: devAddr) : default)
                .Where(a => a is (true, _))
                .Select(a => a.Value)
                .ToHashSet());
            SetPropertyIfDefined((c, v) => c.LnsServerPfxPath = v, "LNS_SERVER_PFX_PATH");
            SetPropertyIfDefined((c, v) => c.LnsServerPfxPassword = v, "LNS_SERVER_PFX_PASSWORD");
            SetGenericIfDefined("CLIENT_CERTIFICATE_MODE", (c, v) => c.ClientCertificateMode = v, "NoCertificate", v => Enum.Parse<ClientCertificateMode>(v, true));
            SetPropertyIfDefined((c, v) => c.LnsVersion = v, "LNS_VERSION");
            SetGenericIfDefined("IOTHUB_CONNECTION_POOL_SIZE", (c, v) => c.IotHubConnectionPoolSize = v, 1U, v => v is uint size
                && size > 0U
                && size < AmqpConnectionPoolSettings.AbsoluteMaxPoolSize
                ? size
                : throw new NotSupportedException($"'IOTHUB_CONNECTION_POOL_SIZE' needs to be between 1 and {AmqpConnectionPoolSettings.AbsoluteMaxPoolSize}."));

            SetPropertyIfDefined((c, v) => c.RedisConnectionString = v, "REDIS_CONNECTION_STRING");

            if (!networkServerConfiguration.RunningAsIoTEdgeModule && string.IsNullOrEmpty(networkServerConfiguration.RedisConnectionString))
                throw new InvalidOperationException("'REDIS_CONNECTION_STRING' can't be empty if running network server as part of a cloud only deployment.");

            return networkServerConfiguration;
        }
    }
}
