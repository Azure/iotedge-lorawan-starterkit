// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.ModuleConnection
{
    using LoRaTools.Utils;
    using LoRaTools;
    using LoRaWan.NetworkServer;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Configuration;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class ModuleConnectionHost : IAsyncDisposable
    {
        private const string LnsVersionPropertyName = "LnsVersion";
        private readonly NetworkServerConfiguration networkServerConfiguration;
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        private readonly ILnsRemoteCallHandler lnsRemoteCallHandler;
        private readonly ILogger<ModuleConnectionHost> logger;
        private readonly Counter<int> unhandledExceptionCount;
        private ILoraModuleClient loRaModuleClient;
        private readonly ILoRaModuleClientFactory loRaModuleClientFactory;

        public ModuleConnectionHost(
            NetworkServerConfiguration networkServerConfiguration,
            ILoRaModuleClientFactory loRaModuleClientFactory,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILnsRemoteCallHandler lnsRemoteCallHandler,
            ILogger<ModuleConnectionHost> logger,
            Meter meter)
        {
            this.networkServerConfiguration = networkServerConfiguration ?? throw new ArgumentNullException(nameof(networkServerConfiguration));
            this.loRaDeviceAPIService = loRaDeviceAPIService ?? throw new ArgumentNullException(nameof(loRaDeviceAPIService));
            this.lnsRemoteCallHandler = lnsRemoteCallHandler ?? throw new ArgumentNullException(nameof(lnsRemoteCallHandler));
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

        // handlers on device -- to be replaced with redis subscriber
        internal async Task<MethodResponse> OnDirectMethodCalled(MethodRequest methodRequest, object userContext)
        {
            if (methodRequest == null) throw new ArgumentNullException(nameof(methodRequest));

            try
            {
                using var cts = methodRequest.ResponseTimeout is { } someResponseTimeout ? new CancellationTokenSource(someResponseTimeout) : null;
                var token = cts?.Token ?? CancellationToken.None;

                // Mapping via the constants for backwards compatibility.
                LnsRemoteCall lnsRemoteCall;
                if (string.Equals(NetworkServer.Constants.CloudToDeviceClearCache, methodRequest.Name, StringComparison.OrdinalIgnoreCase))
                {
                    lnsRemoteCall = new LnsRemoteCall(RemoteCallKind.ClearCache, null);
                }
                else if (string.Equals(NetworkServer.Constants.CloudToDeviceCloseConnection, methodRequest.Name, StringComparison.OrdinalIgnoreCase))
                {
                    lnsRemoteCall = new LnsRemoteCall(RemoteCallKind.CloseConnection, methodRequest.DataAsJson);
                }
                else if (string.Equals(NetworkServer.Constants.CloudToDeviceDecoderElementName, methodRequest.Name, StringComparison.OrdinalIgnoreCase))
                {
                    lnsRemoteCall = new LnsRemoteCall(RemoteCallKind.CloudToDeviceMessage, methodRequest.DataAsJson);
                }
                else
                {
                    throw new LoRaProcessingException($"Unknown direct method called: {methodRequest.Name}");
                }

                var statusCode = await lnsRemoteCallHandler.ExecuteAsync(lnsRemoteCall, token);
                return new MethodResponse((int)statusCode);
            }
            catch (Exception ex) when (ExceptionFilterUtility.False(() => this.logger.LogError(ex, $"An exception occurred on a direct method call: {ex}"),
                                                                    () => this.unhandledExceptionCount.Add(1)))
            {
                throw;
            }
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

            if (reader.TryRead<int>(NetworkServer.Constants.ProcessingDelayKey, out var processingDelay))
            {
                if (processingDelay >= 0)
                {
                    this.logger.LogDebug("Updating processing delay for LNS to {ProcessingDelay} from desired properties of the module twin", processingDelay);
                    this.networkServerConfiguration.ProcessingDelayInMilliseconds = processingDelay;
                }
                else
                {
                    this.logger.LogError("Processing delay for LNS was set to an invalid value {ProcessingDelay}, " +
                        "using default delay of {DefaultDelay} ms", processingDelay, NetworkServer.Constants.DefaultProcessingDelayInMilliseconds);
                }
            }

            if (reader.TryRead<string>(NetworkServer.Constants.FacadeServerUrlKey, out var faceServerUrl))
            {
                if (Uri.TryCreate(faceServerUrl, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
                {
                    this.loRaDeviceAPIService.URL = url;
                    if (reader.TryRead<string>(NetworkServer.Constants.FacadeServerAuthCodeKey, out var authCode))
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
