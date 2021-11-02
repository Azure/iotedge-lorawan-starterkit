// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.ModuleConnection
{
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class ModuleConnectionHost : IAsyncDisposable
    {
        private readonly NetworkServerConfiguration networkServerConfiguration;
        private readonly IClassCDeviceMessageSender classCMessageSender;
        private readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        private readonly ILoraModuleClient loRaModuleClient;

        public ModuleConnectionHost(
            NetworkServerConfiguration networkServerConfiguration,
            IClassCDeviceMessageSender defaultClassCDevicesMessageSender,
            ILoRaDeviceRegistry loRaDeviceRegistry)
        {
            this.networkServerConfiguration = networkServerConfiguration ?? throw new ArgumentNullException(nameof(networkServerConfiguration));
            this.classCMessageSender = defaultClassCDevicesMessageSender ?? throw new ArgumentNullException(nameof(defaultClassCDevicesMessageSender));
            this.loRaDeviceRegistry = loRaDeviceRegistry ?? throw new ArgumentNullException(nameof(loRaDeviceRegistry));
            this.loRaModuleClient = new LoRaModuleClient();
        }

        /// <summary>
        /// This constructor is only to be used by tests.
        /// </summary>
        internal ModuleConnectionHost(
           NetworkServerConfiguration networkServerConfiguration,
           IClassCDeviceMessageSender defaultClassCDevicesMessageSender,
           ILoRaDeviceRegistry loRaDeviceRegistry,
           ILoraModuleClient loraModuleClient)
        {
            this.networkServerConfiguration = networkServerConfiguration ?? throw new ArgumentNullException(nameof(networkServerConfiguration));
            this.classCMessageSender = defaultClassCDevicesMessageSender ?? throw new ArgumentNullException(nameof(defaultClassCDevicesMessageSender));
            this.loRaDeviceRegistry = loRaDeviceRegistry ?? throw new ArgumentNullException(nameof(loRaDeviceRegistry));
            this.loRaModuleClient = loraModuleClient;
        }

        public async Task CreateAsync(CancellationToken cancellationToken)
        {
            ITransportSettings[] settings = { new AmqpTransportSettings(Microsoft.Azure.Devices.Client.TransportType.Amqp_Tcp_Only) };

            await this.loRaModuleClient.CreateFromEnvironmentAsync(settings);
            await InitModuleAsync(cancellationToken);

        }

        internal async Task InitModuleAsync(CancellationToken cancellationToken)
        { 
            // Obsolete, this should be removed as part of #456
            Logger.Init(new LoggerConfiguration
            {
                ModuleClient = this.loRaModuleClient.GetModuleClient(),
                LogLevel = LoggerConfiguration.InitLogLevel(networkServerConfiguration.LogLevel),
                LogToConsole = networkServerConfiguration.LogToConsole,
                LogToHub = networkServerConfiguration.LogToHub,
                LogToUdp = networkServerConfiguration.LogToUdp,
                LogToUdpPort = networkServerConfiguration.LogToUdpPort,
                LogToUdpAddress = networkServerConfiguration.LogToUdpAddress,
                GatewayId = networkServerConfiguration.GatewayID
            });

            if (networkServerConfiguration.IoTEdgeTimeout > 0)
            {
                this.loRaModuleClient.OperationTimeout = TimeSpan.FromMilliseconds(networkServerConfiguration.IoTEdgeTimeout);
                Logger.Log($"Changing timeout to {networkServerConfiguration.IoTEdgeTimeout} ms", LogLevel.Debug);
            }

            Logger.Log("Getting properties from module twin...", LogLevel.Information);
            Twin moduleTwin;
            try
            {
                moduleTwin = await this.loRaModuleClient.GetTwinAsync(cancellationToken);
            }
            catch (IotHubCommunicationException)
            {
                Logger.Log($"There was a critical problem with the IoT Hub in getting the module twins.", LogLevel.Error);
                throw;
            }

            var moduleTwinCollection = moduleTwin.Properties.Desired;

            if (!TryUpdateConfigurationWithDesiredProperties(moduleTwinCollection))
            {
                Logger.Log($"The initial configuration of the facade function could not be found in the desired properties", LogLevel.Error);
                throw new ArgumentException("Could not get Facade information from module twin");
            }

            await this.loRaModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

            await this.loRaModuleClient.SetMethodDefaultHandlerAsync(OnDirectMethodCalled, null);
        }

        internal async Task<MethodResponse> OnDirectMethodCalled(MethodRequest methodRequest, object userContext)
        {
            if (string.Equals(Constants.CloudToDeviceClearCache, methodRequest.Name, StringComparison.OrdinalIgnoreCase))
            {
                return await ClearCache();
            }
            else if (string.Equals(Constants.CloudToDeviceDecoderElementName, methodRequest.Name, StringComparison.OrdinalIgnoreCase))
            {
                return await SendCloudToDeviceMessageAsync(methodRequest);
            }

            Logger.Log($"Unknown direct method called: {methodRequest?.Name}", LogLevel.Error);

            return new MethodResponse((int)HttpStatusCode.NotFound);
        }

        internal async Task<MethodResponse> SendCloudToDeviceMessageAsync(MethodRequest methodRequest)
        {
            if (!string.IsNullOrEmpty(methodRequest.DataAsJson))
            {
                ReceivedLoRaCloudToDeviceMessage c2d = null;

                try
                {
                    c2d = JsonSerializer.Deserialize<ReceivedLoRaCloudToDeviceMessage>(methodRequest.DataAsJson);
                }
                catch (JsonException ex)
                {
                    Logger.Log($"Impossible to parse Json for c2d message {c2d}, error: {ex}", LogLevel.Error);
                    return new MethodResponse((int)HttpStatusCode.BadRequest);
                }


                Logger.Log(c2d.DevEUI, $"received cloud to device message from direct method: {methodRequest.DataAsJson}", LogLevel.Debug);

                using var cts = methodRequest.ResponseTimeout.HasValue ? new CancellationTokenSource(methodRequest.ResponseTimeout.Value) : null;

                if (await this.classCMessageSender.SendAsync(c2d, cts?.Token ?? CancellationToken.None))
                {
                    return new MethodResponse((int)HttpStatusCode.OK);
                }
            }

            return new MethodResponse((int)HttpStatusCode.BadRequest);
        }

        private Task<MethodResponse> ClearCache()
        {
            this.loRaDeviceRegistry.ResetDeviceCache();

            return Task.FromResult(new MethodResponse((int)HttpStatusCode.OK));
        }

        /// <summary>
        /// Method to update the desired properties.
        /// We only want to update the auth code if the facadeUri was performed.
        /// If the update is not acceptable we don't apply it.
        /// </summary>
        internal Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                _ = TryUpdateConfigurationWithDesiredProperties(desiredProperties);
            } catch (ArgumentOutOfRangeException ex)
            {
                Logger.Log($"A desired properties update was detected but the parameters are out of range with exception :  {ex}", LogLevel.Warning);
            }

            return Task.CompletedTask;
        }

        private bool TryUpdateConfigurationWithDesiredProperties(TwinCollection desiredProperties)
        {
            if (desiredProperties.Contains(Constants.FacadeServerUrlKey)
                && (string)desiredProperties[Constants.FacadeServerUrlKey] is { Length: > 0 } urlString)
            {
                if (Uri.TryCreate(urlString, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
                {
                    this.networkServerConfiguration.FacadeServerUrl = url;
                    if (desiredProperties.Contains(Constants.FacadeServerAuthCodeKey))
                    {
                        this.networkServerConfiguration.FacadeAuthCode = (string)desiredProperties[Constants.FacadeServerAuthCodeKey];
                    }

                    Logger.Log("Desired property changed", LogLevel.Debug);
                    return true;
                }
                else
                {
                    Logger.Log("The Facade server Url present in device desired properties was malformed.", LogLevel.Error);
                    throw new ArgumentOutOfRangeException(nameof(desiredProperties));
                }
            }

            Logger.Log("no desired property changed", LogLevel.Debug);
            return false;

        }

        public async ValueTask DisposeAsync()
        {
            await this.loRaModuleClient.DisposeAsync();
        }
    }
}
