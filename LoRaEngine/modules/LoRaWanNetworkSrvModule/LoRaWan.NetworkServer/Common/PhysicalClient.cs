// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Common
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using BasicStation;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.NetworkServer.PacketForwarder;
    using LoRaWan.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public abstract class PhysicalClient : IPhysicalClient
    {
#pragma warning disable SA1401 // Fields should be private
        protected readonly NetworkServerConfiguration configuration;
        protected readonly MessageDispatcher messageDispatcher;
        protected readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        protected readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        protected ModuleClient ioTHubModuleClient;
        protected IClassCDeviceMessageSender classCMessageSender;
#pragma warning restore SA1401 // Fields should be private

        public PhysicalClient(
            NetworkServerConfiguration configuration,
            MessageDispatcher messageDispatcher,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILoRaDeviceRegistry loRaDeviceRegistry)
        {
            this.configuration = configuration;
            this.messageDispatcher = messageDispatcher;
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.loRaDeviceRegistry = loRaDeviceRegistry;
        }

        /// <summary>
        /// Factory method to instantiate the correct UDP endpoint.
        /// </summary>
        public static IPhysicalClient Create()
        {
            var configuration = NetworkServerConfiguration.CreateFromEnviromentVariables();
            var loRaDeviceAPIService = new LoRaDeviceAPIService(configuration, new ServiceFacadeHttpClientProvider(configuration, ApiVersion.LatestVersion));
            var frameCounterStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(configuration.GatewayID, loRaDeviceAPIService);
            var deduplicationStrategyFactory = new DeduplicationStrategyFactory(loRaDeviceAPIService);
            var adrStrategyProvider = new LoRaADRStrategyProvider();
            var cache = new MemoryCache(new MemoryCacheOptions());
            var dataHandlerImplementation = new DefaultLoRaDataRequestHandler(configuration, frameCounterStrategyProvider, new LoRaPayloadDecoder(), deduplicationStrategyFactory, adrStrategyProvider, new LoRAADRManagerFactory(loRaDeviceAPIService), new FunctionBundlerProvider(loRaDeviceAPIService));
            var connectionManager = new LoRaDeviceClientConnectionManager(cache);
            var loRaDeviceFactory = new LoRaDeviceFactory(configuration, dataHandlerImplementation, connectionManager);
            var loRaDeviceRegistry = new LoRaDeviceRegistry(configuration, cache, loRaDeviceAPIService, loRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(configuration, loRaDeviceRegistry, frameCounterStrategyProvider);

            if (configuration.UseBasicStation)
            {
                Logger.Log("Using physical client with basic station implementation", LogLevel.Information);
                return new BasicStation(configuration, messageDispatcher, loRaDeviceAPIService, loRaDeviceRegistry);
            }
            else
            {
                Logger.Log("Using physical client with the packet forwarder implementation", LogLevel.Information);
                var udpServer = new UdpServer(configuration, messageDispatcher, loRaDeviceAPIService, loRaDeviceRegistry);

                // TODO: review dependencies
                var classCMessageSender = new DefaultClassCDevicesMessageSender(configuration, loRaDeviceRegistry, udpServer, frameCounterStrategyProvider);
                dataHandlerImplementation.SetClassCMessageSender(classCMessageSender);

                udpServer.SetClassCMessageSender(classCMessageSender);
                return udpServer;
            }
        }

        async Task InitCallBack()
        {
            try
            {
                ITransportSettings transportSettings = new AmqpTransportSettings(Microsoft.Azure.Devices.Client.TransportType.Amqp_Tcp_Only);

                ITransportSettings[] settings = { transportSettings };

                // if running as Edge module
                if (this.configuration.RunningAsIoTEdgeModule)
                {
                    this.ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

                    Logger.Init(new LoggerConfiguration
                    {
                        ModuleClient = this.ioTHubModuleClient,
                        LogLevel = LoggerConfiguration.InitLogLevel(this.configuration.LogLevel),
                        LogToConsole = this.configuration.LogToConsole,
                        LogToHub = this.configuration.LogToHub,
                        LogToUdp = this.configuration.LogToUdp,
                        LogToUdpPort = this.configuration.LogToUdpPort,
                        LogToUdpAddress = this.configuration.LogToUdpAddress,
                        GatewayId = this.configuration.GatewayID
                    });

                    if (this.configuration.IoTEdgeTimeout > 0)
                    {
                        this.ioTHubModuleClient.OperationTimeoutInMilliseconds = this.configuration.IoTEdgeTimeout;
                        Logger.Log($"Changing timeout to {this.ioTHubModuleClient.OperationTimeoutInMilliseconds} ms", LogLevel.Debug);
                    }

                    Logger.Log("Getting properties from module twin...", LogLevel.Information);

                    var moduleTwin = await this.ioTHubModuleClient.GetTwinAsync();
                    var moduleTwinCollection = moduleTwin.Properties.Desired;
                    try
                    {
                        this.loRaDeviceAPIService.SetURL(moduleTwinCollection["FacadeServerUrl"].Value as string);
                        Logger.LogAlways($"Facade function url: {this.loRaDeviceAPIService.URL}");
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Logger.Log("Module twin FacadeServerUrl property does not exist", LogLevel.Error);
                        throw e;
                    }

                    try
                    {
                        this.loRaDeviceAPIService.SetAuthCode(moduleTwinCollection["FacadeAuthCode"].Value as string);
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Logger.Log("Module twin FacadeAuthCode does not exist", LogLevel.Error);
                        throw e;
                    }

                    await this.ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(this.OnDesiredPropertiesUpdate, null);

                    await this.ioTHubModuleClient.SetMethodDefaultHandlerAsync(this.OnDirectMethodCalled, null);
                }

                // running as non edge module for test and debugging
                else
                {
                    Logger.Init(new LoggerConfiguration
                    {
                        ModuleClient = null,
                        LogLevel = LoggerConfiguration.InitLogLevel(this.configuration.LogLevel),
                        LogToConsole = this.configuration.LogToConsole,
                        LogToHub = this.configuration.LogToHub,
                        LogToUdp = this.configuration.LogToUdp,
                        LogToUdpPort = this.configuration.LogToUdpPort,
                        LogToUdpAddress = this.configuration.LogToUdpAddress,
                        GatewayId = this.configuration.GatewayID
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Initialization failed with error: {ex.Message}", LogLevel.Error);
                throw ex;
            }

            // Report Log level
            Logger.LogAlways($"Log Level: {(LogLevel)Logger.LoggerLevel}");
        }

        async Task<MethodResponse> OnDirectMethodCalled(MethodRequest methodRequest, object userContext)
        {
            if (string.Equals("clearcache", methodRequest.Name, StringComparison.InvariantCultureIgnoreCase))
            {
                return await this.ClearCache(methodRequest, userContext);
            }
            else if (string.Equals(Constants.CLOUD_TO_DEVICE_DECODER_ELEMENT_NAME, methodRequest.Name, StringComparison.InvariantCultureIgnoreCase))
            {
                return await this.SendCloudToDeviceMessageAsync(methodRequest);
            }

            Logger.Log($"Unknown direct method called: {methodRequest?.Name}", LogLevel.Error);

            return new MethodResponse(404);
        }

        private async Task<MethodResponse> SendCloudToDeviceMessageAsync(MethodRequest methodRequest)
        {
            if (this.classCMessageSender == null)
            {
                return new MethodResponse((int)HttpStatusCode.NotFound);
            }

            var c2d = JsonConvert.DeserializeObject<ReceivedLoRaCloudToDeviceMessage>(methodRequest.DataAsJson);
            Logger.Log(c2d.DevEUI, $"received cloud to device message from direct method: {methodRequest.DataAsJson}", LogLevel.Debug);

            CancellationToken cts = CancellationToken.None;
            if (methodRequest.ResponseTimeout.HasValue)
                cts = new CancellationTokenSource(methodRequest.ResponseTimeout.Value).Token;

            if (await this.classCMessageSender.SendAsync(c2d, cts))
            {
                return new MethodResponse((int)HttpStatusCode.OK);
            }
            else
            {
                return new MethodResponse((int)HttpStatusCode.BadRequest);
            }
        }

        private Task<MethodResponse> ClearCache(MethodRequest methodRequest, object userContext)
        {
            this.loRaDeviceRegistry.ResetDeviceCache();

            return Task.FromResult(new MethodResponse(200));
        }

        Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                if (desiredProperties.Contains("FacadeServerUrl"))
                {
                    this.loRaDeviceAPIService.SetURL((string)desiredProperties["FacadeServerUrl"]);
                }

                if (desiredProperties.Contains("FacadeAuthCode"))
                {
                    this.loRaDeviceAPIService.SetAuthCode((string)desiredProperties["FacadeAuthCode"]);
                }

                Logger.Log("Desired property changed", LogLevel.Debug);
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Logger.Log($"Error when receiving desired property: {exception}", LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error when receiving desired property: {ex.Message}", LogLevel.Error);
            }

            return Task.CompletedTask;
        }

        public abstract void Dispose();

        public abstract Task RunServerProcess(CancellationToken cancellationToken);

        public async Task RunServer(CancellationToken cancellationToken = default)
        {
            Logger.LogAlways("Starting LoRaWAN Server...");

            await this.InitCallBack();

            await this.RunServerProcess(cancellationToken);
        }
    }
}
