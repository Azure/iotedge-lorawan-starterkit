// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
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
        public static NetworkServerConfiguration Create(IConfiguration configuration) =>
            new NetworkServerConfigurationBuilder(configuration).Build();

        private sealed class NetworkServerConfigurationBuilder
        {
            private readonly IConfiguration configuration;
            private readonly NetworkServerConfiguration instance;

            public NetworkServerConfigurationBuilder(IConfiguration configuration)
            {
                this.configuration = configuration;
                this.instance = new NetworkServerConfiguration();
            }

            public NetworkServerConfiguration Build()
            {
                // Create case insensitive dictionary from environment variables
                SetProperty("PROCESSING_DELAY_IN_MS", c => c.ProcessingDelayInMilliseconds, Constants.DefaultProcessingDelayInMilliseconds);
                SetProperty("CLOUD_DEPLOYMENT", c => c.RunningAsIoTEdgeModule, false, v => !v);

                SetProperty(c => c.IoTHubHostName, "IOTEDGE_IOTHUBHOSTNAME", "IOTHUBHOSTNAME");
                if (string.IsNullOrEmpty(this.instance.IoTHubHostName))
                    throw new InvalidOperationException("Either 'IOTEDGE_IOTHUBHOSTNAME' or 'IOTHUBHOSTNAME' environment variable should be populated");

                SetProperty(c => c.GatewayHostName, "IOTEDGE_GATEWAYHOSTNAME");
                SetProperty("ENABLE_GATEWAY", c => c.EnableGateway, true);
                if (!this.instance.RunningAsIoTEdgeModule && this.instance.EnableGateway)
                    throw new NotSupportedException("ENABLE_GATEWAY cannot be true if RunningAsIoTEdgeModule is false.");

                SetProperty(c => c.GatewayID, "IOTEDGE_DEVICEID", "HOSTNAME");
                if (string.IsNullOrEmpty(this.instance.GatewayID))
                    throw new InvalidOperationException("Either 'IOTEDGE_DEVICEID' or 'HOSTNAME' environment variable should be populated");

                SetProperty(c => c.HttpsProxy, "HTTPS_PROXY");
                SetProperty("RX2_DATR", c => c.Rx2DataRate, -1, v => v is var datrNum && (DataRateIndex)datrNum is var datr && Enum.IsDefined(datr) ? datr : null);
                SetProperty<double?, Hertz?>("RX2_FREQ", c => c.Rx2Frequency, null, v => v is { } someFreq ? Hertz.Mega(someFreq) : null);

                SetProperty("IOTEDGE_TIMEOUT", c => c.IoTEdgeTimeout, this.instance.IoTEdgeTimeout);

                // facadeurl is allowed to be null as the value is coming from the twin in production.
                SetProperty<string, Uri>("FACADE_SERVER_URL", c => c.FacadeServerUrl, null, v => string.IsNullOrEmpty(v) ? null : new Uri(v));
                SetProperty(c => c.FacadeAuthCode, "FACADE_AUTH_CODE");
                SetProperty(c => c.LogLevel, "LOG_LEVEL");
                SetProperty("LOG_TO_CONSOLE", c => c.LogToConsole, false);
                SetProperty("LOG_TO_TCP", c => c.LogToTcp, false);
                SetProperty("LOG_TO_HUB", c => c.LogToHub, false);
                SetProperty(c => c.LogToTcpAddress, "LOG_TO_TCP_ADDRESS");
                SetProperty("LOG_TO_TCP_PORT", c => c.LogToTcpPort, 6000);
                SetProperty("NETID", c => c.NetId, 1, v => new NetId(v));
                SetProperty("AllowedDevAddresses", c => c.AllowedDevAddresses, string.Empty, v => v.Split(";")
                    .Select(s => DevAddr.TryParse(s, out var devAddr) ? (true, Value: devAddr) : default)
                    .Where(a => a is (true, _))
                    .Select(a => a.Value)
                    .ToHashSet());
                SetProperty(c => c.LnsServerPfxPath, "LNS_SERVER_PFX_PATH");
                SetProperty(c => c.LnsServerPfxPassword, "LNS_SERVER_PFX_PASSWORD");
                SetProperty("CLIENT_CERTIFICATE_MODE", c => c.ClientCertificateMode, "NoCertificate", v => Enum.Parse<ClientCertificateMode>(v, true));
                SetProperty(c => c.LnsVersion, "LNS_VERSION");
                SetProperty("IOTHUB_CONNECTION_POOL_SIZE", c => c.IotHubConnectionPoolSize, 1U, v => v is uint size
                    && size > 0U
                    && size < AmqpConnectionPoolSettings.AbsoluteMaxPoolSize
                    ? size
                    : throw new NotSupportedException($"'IOTHUB_CONNECTION_POOL_SIZE' needs to be between 1 and {AmqpConnectionPoolSettings.AbsoluteMaxPoolSize}."));

                SetProperty(c => c.RedisConnectionString, "REDIS_CONNECTION_STRING");

                if (!this.instance.RunningAsIoTEdgeModule && string.IsNullOrEmpty(this.instance.RedisConnectionString))
                    throw new InvalidOperationException("'REDIS_CONNECTION_STRING' can't be empty if running network server as part of a cloud only deployment.");

                return this.instance;
            }

            private void SetPropertyValue<T>(Expression<Func<NetworkServerConfiguration, T>> memberLamda,
                                             T value)
            {
                if (memberLamda.Body is MemberExpression memberSelectorExpression)
                {
                    var property = memberSelectorExpression.Member as PropertyInfo;
                    property.SetValue(this.instance, value, null);
                }
            }

            private void SetProperty(Expression<Func<NetworkServerConfiguration, string>> memberLambda,
                                     params string[] configurationValueNames)
            {
                foreach (var configurationValueName in configurationValueNames)
                {
                    var value = configuration.GetValue<string>(configurationValueName);

                    if (!string.IsNullOrEmpty(value))
                    {
                        SetPropertyValue(memberLambda, value);
                        break;
                    }
                }
            }

            private void SetProperty<T, U>(string configurationValueName,
                                           Expression<Func<NetworkServerConfiguration, U>> propertySelector,
                                           T defaultValue,
                                           Func<T, U> selector) =>
                SetPropertyValue(propertySelector, selector(configuration.GetValue(configurationValueName, defaultValue)));

            private void SetProperty<T>(string configurationValueName,
                                        Expression<Func<NetworkServerConfiguration, T>> propertySelector,
                                        T defaultValue) =>
                SetProperty(configurationValueName, propertySelector, defaultValue, v => v);
        }
    }
}
