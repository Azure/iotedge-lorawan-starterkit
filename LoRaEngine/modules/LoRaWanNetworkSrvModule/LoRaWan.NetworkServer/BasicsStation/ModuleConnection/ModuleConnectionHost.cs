// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.ModuleConnection
{
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer.Logger;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Configuration;
    using System.Diagnostics.Metrics;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class ModuleConnectionHost : IAsyncDisposable
    {
        private const string LnsVersionPropertyName = "LnsVersion";
        private readonly NetworkServerConfiguration networkServerConfiguration;
        private readonly IClassCDeviceMessageSender classCMessageSender;
        private readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        private readonly ILogger<ModuleConnectionHost> logger;
        private readonly Counter<int> unhandledExceptionCount;
        private ILoraModuleClient loRaModuleClient;
        private readonly ILoRaModuleClientFactory loRaModuleClientFactory;

        public ModuleConnectionHost(
            NetworkServerConfiguration networkServerConfiguration,
            IClassCDeviceMessageSender defaultClassCDevicesMessageSender,
            ILoRaModuleClientFactory loRaModuleClientFactory,
            ILoRaDeviceRegistry loRaDeviceRegistry,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILogger<ModuleConnectionHost> logger,
            Meter meter)
        {
            this.networkServerConfiguration = networkServerConfiguration ?? throw new ArgumentNullException(nameof(networkServerConfiguration));
            this.classCMessageSender = defaultClassCDevicesMessageSender ?? throw new ArgumentNullException(nameof(defaultClassCDevicesMessageSender));
            this.loRaDeviceRegistry = loRaDeviceRegistry ?? throw new ArgumentNullException(nameof(loRaDeviceRegistry));
            this.loRaDeviceAPIService = loRaDeviceAPIService ?? throw new ArgumentNullException(nameof(loRaDeviceAPIService));
            this.loRaModuleClientFactory = loRaModuleClientFactory ?? throw new ArgumentNullException(nameof(loRaModuleClientFactory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.unhandledExceptionCount = (meter ?? throw new ArgumentNullException(nameof(meter))).CreateCounter<int>(MetricRegistry.UnhandledExceptions);
        }

        public async Task CreateAsync(CancellationToken cancellationToken)
        {
            this.loRaModuleClient = await this.loRaModuleClientFactory.CreateAsync();
            await InitModuleAsync(cancellationToken);
            await this.loRaModuleClient.UpdateReportedPropertyAsync(LnsVersionPropertyName, this.networkServerConfiguration.LnsVersion);
        }

        internal async Task InitModuleAsync(CancellationToken cancellationToken)
        {
            if (networkServerConfiguration.IoTEdgeTimeout > 0)
            {
                this.loRaModuleClient.OperationTimeout = TimeSpan.FromMilliseconds(networkServerConfiguration.IoTEdgeTimeout);
                this.logger.LogDebug($"Changing timeout to {networkServerConfiguration.IoTEdgeTimeout} ms");
            }

            this.logger.LogInformation("Getting properties from module twin...");
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
                this.logger.LogError("The initial configuration of the facade function could not be found in the desired properties");
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
                    return await ClearCacheAsync();
                }
                else if (string.Equals(Constants.CloudToDeviceDropConnection, methodRequest.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return await DropConnection(methodRequest);
                }
                else if (string.Equals(Constants.CloudToDeviceDecoderElementName, methodRequest.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return await SendCloudToDeviceMessageAsync(methodRequest);
                }

                this.logger.LogError($"Unknown direct method called: {methodRequest.Name}");

                return new MethodResponse((int)HttpStatusCode.BadRequest);
            }
            catch (Exception ex) when (ExceptionFilterUtility.False(() => this.logger.LogError(ex, $"An exception occurred on a direct method call: {ex}"),
                                                                    () => this.unhandledExceptionCount.Add(1)))
            {
                throw;
            }
        }

        private async Task<MethodResponse> SendCloudToDeviceMessageAsync(MethodRequest methodRequest)
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
                    this.logger.LogError($"Impossible to parse Json for c2d message for device {c2d?.DevEUI}, error: {ex}");
                    return new MethodResponse((int)HttpStatusCode.BadRequest);
                }

                using var cts = methodRequest.ResponseTimeout.HasValue ? new CancellationTokenSource(methodRequest.ResponseTimeout.Value) : null;

                using var scope = this.logger.BeginDeviceScope(c2d.DevEUI);
                this.logger.LogDebug($"received cloud to device message from direct method: {methodRequest.DataAsJson}");

                if (await this.classCMessageSender.SendAsync(c2d, cts?.Token ?? CancellationToken.None))
                {
                    return new MethodResponse((int)HttpStatusCode.OK);
                }
            }

            return new MethodResponse((int)HttpStatusCode.BadRequest);
        }

        private async Task<MethodResponse> ClearCacheAsync()
        {
            await this.loRaDeviceRegistry.ResetDeviceCacheAsync();
            return new MethodResponse((int)HttpStatusCode.OK);
        }

        private async Task<MethodResponse> DropConnection(MethodRequest methodRequest)
        {
            ReceivedLoRaCloudToDeviceMessage c2d = null;

            try
            {
                c2d = JsonSerializer.Deserialize<ReceivedLoRaCloudToDeviceMessage>(methodRequest.DataAsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex) when (ex is ArgumentNullException or JsonException)
            {
                this.logger.LogError($"Impossible to parse Json for c2d message for device {c2d?.DevEUI}, error: {ex}");
                return new MethodResponse((int)HttpStatusCode.BadRequest);
            }

            using var cts = methodRequest.ResponseTimeout.HasValue ? new CancellationTokenSource(methodRequest.ResponseTimeout.Value) : null;

            if (c2d.DevEUI == null)
            {
                this.logger.LogError($"DevEUI missing, cannot identify device to drop connection for; message Id: {c2d.MessageId}");
                return new MethodResponse((int)HttpStatusCode.BadRequest);
            }

            using var scope = this.logger.BeginDeviceScope(c2d.DevEUI);

            var loRaDevice = await this.loRaDeviceRegistry.GetDeviceByDevEUIAsync(c2d.DevEUI.Value);
            await loRaDevice.CloseConnectionAsync(cts?.Token ?? CancellationToken.None);

            return new MethodResponse((int)HttpStatusCode.OK);
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
                this.logger.LogWarning($"A desired properties update was detected but the parameters are out of range with exception :  {ex}");
            }
            catch (Exception ex) when (ExceptionFilterUtility.False(() => this.logger.LogError(ex, $"An exception occurred on desired property update: {ex}"),
                                                                    () => this.unhandledExceptionCount.Add(1)))
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

            var reader = new TwinCollectionReader(desiredProperties, this.logger);
            if (reader.TryRead<string>(Constants.FacadeServerUrlKey, out var faceServerUrl))
            {
                if (Uri.TryCreate(faceServerUrl, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
                {
                    this.loRaDeviceAPIService.URL = url;
                    if (reader.TryRead<string>(Constants.FacadeServerAuthCodeKey, out var authCode))
                    {
                        this.loRaDeviceAPIService.SetAuthCode(authCode);
                    }

                    return true;
                }
                else
                {
                    this.logger.LogError("The Facade server Url present in device desired properties was malformed.");
                    throw new ConfigurationErrorsException(nameof(desiredProperties));
                }
            }

            this.logger.LogDebug("no desired property changed");
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
