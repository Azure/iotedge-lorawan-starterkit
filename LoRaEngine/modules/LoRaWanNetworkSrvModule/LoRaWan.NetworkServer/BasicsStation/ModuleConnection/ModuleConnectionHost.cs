// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.ModuleConnection
{
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Configuration;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class ModuleConnectionHost : IAsyncDisposable
    {
        private readonly NetworkServerConfiguration networkServerConfiguration;
        private readonly IClassCDeviceMessageSender classCMessageSender;
        private readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        private ILoraModuleClient loRaModuleClient;
        private readonly ILoRaModuleClientFactory loRaModuleClientFactory;

        public ModuleConnectionHost(
            NetworkServerConfiguration networkServerConfiguration,
            IClassCDeviceMessageSender defaultClassCDevicesMessageSender,
            ILoRaModuleClientFactory loRaModuleClientFactory,
            ILoRaDeviceRegistry loRaDeviceRegistry,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService)
        {
            this.networkServerConfiguration = networkServerConfiguration ?? throw new ArgumentNullException(nameof(networkServerConfiguration));
            this.classCMessageSender = defaultClassCDevicesMessageSender ?? throw new ArgumentNullException(nameof(defaultClassCDevicesMessageSender));
            this.loRaDeviceRegistry = loRaDeviceRegistry ?? throw new ArgumentNullException(nameof(loRaDeviceRegistry));
            this.loRaDeviceAPIService = loRaDeviceAPIService ?? throw new ArgumentNullException(nameof(loRaDeviceAPIService));
            this.loRaModuleClientFactory = loRaModuleClientFactory ?? throw new ArgumentNullException(nameof(loRaModuleClientFactory));
        }

        public async Task CreateAsync(CancellationToken cancellationToken)
        {
            this.loRaModuleClient = await this.loRaModuleClientFactory.CreateAsync();
            await InitModuleAsync(cancellationToken);
        }

        internal async Task InitModuleAsync(CancellationToken cancellationToken)
        {
            // Obsolete, this should be removed as part of #456
            _ = TcpLogger.Init(new LoggerConfiguration
            {
                LogLevel = LoggerConfiguration.InitLogLevel(networkServerConfiguration.LogLevel),
                LogToConsole = networkServerConfiguration.LogToConsole,
                LogToTcp = networkServerConfiguration.LogToTcp,
                LogToTcpPort = networkServerConfiguration.LogToTcpPort,
                LogToTcpAddress = networkServerConfiguration.LogToTcpAddress,
                GatewayId = networkServerConfiguration.GatewayID
            });

            if (networkServerConfiguration.IoTEdgeTimeout > 0)
            {
                this.loRaModuleClient.OperationTimeout = TimeSpan.FromMilliseconds(networkServerConfiguration.IoTEdgeTimeout);
                TcpLogger.Log($"Changing timeout to {networkServerConfiguration.IoTEdgeTimeout} ms", LogLevel.Debug);
            }

            TcpLogger.Log("Getting properties from module twin...", LogLevel.Information);
            Twin moduleTwin;
            try
            {
                moduleTwin = await this.loRaModuleClient.GetTwinAsync(cancellationToken);
            }
            catch (IotHubCommunicationException)
            {
                throw new LoRaProcessingException("There was a critical problem with the IoT Hub in getting the module twins.",
                                                  LoRaProcessingErrorCode.TwinFetchFailed);
            }

            var moduleTwinCollection = moduleTwin?.Properties?.Desired;

            if (!TryUpdateConfigurationWithDesiredProperties(moduleTwinCollection))
            {
                TcpLogger.Log($"The initial configuration of the facade function could not be found in the desired properties", LogLevel.Error);
                throw new ConfigurationErrorsException("Could not get Facade information from module twin");
            }

            await this.loRaModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

            await this.loRaModuleClient.SetMethodDefaultHandlerAsync(OnDirectMethodCalled, null);
        }

        internal async Task<MethodResponse> OnDirectMethodCalled(MethodRequest methodRequest, object userContext)
        {
            if (methodRequest == null) throw new ArgumentNullException(nameof(methodRequest));

            try
            {
                if (string.Equals(Constants.CloudToDeviceClearCache, methodRequest.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return await ClearCache();
                }
                else if (string.Equals(Constants.CloudToDeviceDecoderElementName, methodRequest.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return await SendCloudToDeviceMessageAsync(methodRequest);
                }

                TcpLogger.Log($"Unknown direct method called: {methodRequest.Name}", LogLevel.Error);

                return new MethodResponse((int)HttpStatusCode.BadRequest);
            }
            catch (Exception ex) when (ExceptionFilterUtility.False(() => TcpLogger.Log($"An exception occurred on a direct method call: {ex}", LogLevel.Error)))
            {
                throw;
            }
        }

        internal async Task<MethodResponse> SendCloudToDeviceMessageAsync(MethodRequest methodRequest)
        {
            if (!string.IsNullOrEmpty(methodRequest.DataAsJson))
            {
                ReceivedLoRaCloudToDeviceMessage c2d = null;

                try
                {
                    c2d = JsonSerializer.Deserialize<ReceivedLoRaCloudToDeviceMessage>(methodRequest.DataAsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException ex)
                {
                    TcpLogger.Log($"Impossible to parse Json for c2d message for device {c2d?.DevEUI}, error: {ex}", LogLevel.Error);
                    return new MethodResponse((int)HttpStatusCode.BadRequest);
                }

                TcpLogger.Log(c2d.DevEUI, $"received cloud to device message from direct method: {methodRequest.DataAsJson}", LogLevel.Debug);

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
            }
            catch (ConfigurationErrorsException ex)
            {
                TcpLogger.Log($"A desired properties update was detected but the parameters are out of range with exception :  {ex}", LogLevel.Warning);
            }
            catch (Exception ex) when (ExceptionFilterUtility.False(() => TcpLogger.Log($"An exception occurred on desired property update: {ex}", LogLevel.Error)))
            {
                throw;
            }

            return Task.CompletedTask;
        }

        private bool TryUpdateConfigurationWithDesiredProperties(TwinCollection desiredProperties)
        {
            if (desiredProperties is null)
            {
                return false;
            }

            if (desiredProperties.Contains(Constants.FacadeServerUrlKey)
                && (string)desiredProperties[Constants.FacadeServerUrlKey] is { Length: > 0 } urlString)
            {
                if (Uri.TryCreate(urlString, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
                {
                    this.loRaDeviceAPIService.URL = url;
                    if (desiredProperties.Contains(Constants.FacadeServerAuthCodeKey))
                    {
                        this.loRaDeviceAPIService.SetAuthCode((string)desiredProperties[Constants.FacadeServerAuthCodeKey]);
                    }

                    TcpLogger.Log("Desired property changed", LogLevel.Debug);
                    return true;
                }
                else
                {
                    TcpLogger.Log("The Facade server Url present in device desired properties was malformed.", LogLevel.Error);
                    throw new ConfigurationErrorsException(nameof(desiredProperties));
                }
            }

            TcpLogger.Log("no desired property changed", LogLevel.Debug);
            return false;
        }

        public async ValueTask DisposeAsync()
        {
            if (this.loRaModuleClient != null)
            {
                await this.loRaModuleClient.DisposeAsync();
            }
        }
    }
}
