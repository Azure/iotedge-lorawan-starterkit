// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[assembly: Microsoft.Azure.Functions.Extensions.DependencyInjection.FunctionsStartup(typeof(LoraKeysManagerFacade.FacadeStartup))]

namespace LoraKeysManagerFacade
{
    using System;
    using System.Net.Http.Formatting;
    using System.Net.Http.Headers;
    using System.Text.RegularExpressions;
    using LoraKeysManagerFacade.FunctionBundler;
    using LoraKeysManagerFacade.IoTCentralImp;
    using LoraKeysManagerFacade.IoTHubImp;
    using LoRaTools.ADR;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Functions.Extensions.DependencyInjection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using StackExchange.Redis;

    public class FacadeStartup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));

            var configHandler = ConfigHandler.Create(builder);
            var iotHubConnectionString = configHandler.IoTHubConnectionString;
            var iotCentralEndpoint = configHandler.IoTCentralEndpoint;

            if (configHandler.DeviceRegistryMode == DeviceRegistryMode.IoTHub)
            {
                if (iotHubConnectionString == null)
                {
                    throw new Exception($"Missing {ConfigHandler.IoTHubConnectionStringKey} in settings");
                }
            }
            else if (configHandler.DeviceRegistryMode == DeviceRegistryMode.IoTCentral)
            {
                if (iotCentralEndpoint == null)
                {
                    throw new Exception($"Missing {ConfigHandler.IoTCentralEndpointKey} in settings");
                }
            }

            var redisConnectionString = configHandler.RedisConnectionString;
            if (redisConnectionString == null)
            {
                throw new InvalidOperationException($"Missing {ConfigHandler.RedisConnectionStringKey} in settings");
            }

            var redis = ConnectionMultiplexer.Connect(redisConnectionString);
            var redisCache = redis.GetDatabase();
            var deviceCacheStore = new LoRaDeviceCacheRedisStore(redisCache);

            _ = builder.Services
                    .AddSingleton<IServiceClient>(new ServiceClientAdapter(ServiceClient.CreateFromConnectionString(iotHubConnectionString)))
                    .AddSingleton<ILoRaDeviceCacheStore>(deviceCacheStore)
                    .AddSingleton<ILoRaADRManager>(new LoRaADRServerManager(new LoRaADRRedisStore(redisCache), new LoRaADRStrategyProvider(), deviceCacheStore))
                    .AddSingleton<CreateEdgeDevice>()
                    .AddSingleton<DeviceGetter>()
                    .AddSingleton<FCntCacheCheck>()
                    .AddSingleton<FunctionBundlerFunction>()
                    .AddSingleton<IFunctionBundlerExecutionItem, NextFCntDownExecutionItem>()
                    .AddSingleton<IFunctionBundlerExecutionItem, DeduplicationExecutionItem>()
                    .AddSingleton<IFunctionBundlerExecutionItem, ADRExecutionItem>()
                    .AddSingleton<IFunctionBundlerExecutionItem, PreferredGatewayExecutionItem>()
                    .AddSingleton<LoRaDevAddrCache>();


            if (configHandler.DeviceRegistryMode == DeviceRegistryMode.IoTHub)
            {
                var regex = new Regex("HostName=(.*[.]azure[-]devices[.]net);");
                var match = regex.Match(iotHubConnectionString);

                if (!match.Success)
                {
                    throw new ArgumentException($"Bad ConnectionString format for {ConfigHandler.IoTHubConnectionStringKey} in settings");
                }
#pragma warning disable CA2000 // Dispose objects before losing scope
                // Object is handled by DI container.
                builder.Services.AddSingleton<IDeviceRegistryManager>(sp => new IoTHubDeviceRegistryManager(RegistryManager.CreateFromConnectionString(iotHubConnectionString), match.Groups[0].Value));
#pragma warning restore CA2000 // Dispose objects before losing scope
                builder.Services.AddSingleton<IServiceClient>(new ServiceClientAdapter(ServiceClient.CreateFromConnectionString(iotHubConnectionString)));
            }
            else if (configHandler.DeviceRegistryMode == DeviceRegistryMode.IoTCentral)
            {
                _ = builder.Services.AddTransient<IDeviceProvisioningHelper, DeviceProvisioningHelper>(sp =>
                        new DeviceProvisioningHelper(
                                        provisioningScopeId: configHandler.IoTCentralDeviceProvisioningScopeId,
                                        primaryKey: configHandler.IoTCentralSASIoTDevicesPrimaryKey,
                                        secondaryKey: configHandler.IoTCentralSASIoTDevicesSecondaryKey));

                _ = builder.Services.AddHttpClient<IDeviceRegistryManager, IoTCentralDeviceRegistryManager>(client =>
                {
                    client.BaseAddress = new Uri(iotCentralEndpoint);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SharedAccessSignature", configHandler.IoTCentralToken);
                });
                _ = builder.Services.AddSingleton<IDeviceRegistryManager, IoTCentralDeviceRegistryManager>();
            }
        }

        private abstract class ConfigHandler
        {
            internal const string IoTHubConnectionStringKey = "IoTHubConnectionString";
            internal const string RedisConnectionStringKey = "RedisConnectionString";
            internal const string DeviceRegistryModeKey = "DeviceRegistryMode";
            internal const string IoTCentralEndpointKey = "IoTCentralEndpoint";
            internal const string IoTCentralTokenKey = "IoTCentralToken";
            internal const string IoTCentralDeviceProvisioningScopeIdKey = "IoTCentralDeviceProvisioningScopeId";
            internal const string IoTCentralSASIoTDevicesPrimaryKeyKey = "IoTCentralSASIoTDevicesPrimaryKey";
            internal const string IoTCentralSASIoTDevicesSecondaryKeyKey = "IoTCentralSASIoTDevicesSecondaryKey";

            internal static ConfigHandler Create(IFunctionsHostBuilder builder)
            {
                var tempProvider = builder.Services.BuildServiceProvider();
                var config = tempProvider.GetRequiredService<IConfiguration>();

                var redisConnectionString = config.GetConnectionString(RedisConnectionStringKey);
                if (!string.IsNullOrEmpty(redisConnectionString))
                {
                    return new ProductionConfigHandler(config);
                }

                return new LocalConfigHandler();
            }

            internal abstract string RedisConnectionString { get; }

            internal abstract string IoTHubConnectionString { get; }

            internal abstract string IoTCentralToken { get; }

            internal abstract string IoTCentralEndpoint { get; }

            internal abstract string IoTCentralDeviceProvisioningScopeId { get; }

            internal abstract string IoTCentralSASIoTDevicesPrimaryKey { get; }

            internal abstract string IoTCentralSASIoTDevicesSecondaryKey { get; }

            internal abstract DeviceRegistryMode DeviceRegistryMode { get; }

            class ProductionConfigHandler : ConfigHandler
            {
                private readonly IConfiguration config;

                internal ProductionConfigHandler(IConfiguration config)
                {
                    this.config = config;

                    if (this.DeviceRegistryMode == DeviceRegistryMode.IoTHub && string.IsNullOrEmpty(this.IoTHubConnectionString))
                    {
                        throw new InvalidOperationException($"Device Registry Mode is set to {nameof(DeviceRegistryMode.IoTHub)}, please set the {IoTHubConnectionStringKey} ConnectionString.");
                    }

                    if (this.DeviceRegistryMode == DeviceRegistryMode.IoTCentral && string.IsNullOrEmpty(this.IoTCentralEndpoint))
                    {
                        throw new InvalidOperationException($"Device Registry Mode is set to {nameof(DeviceRegistryMode.IoTCentral)}, please set the {IoTCentralEndpointKey} ConnectionString.");
                    }

                    if (this.DeviceRegistryMode == DeviceRegistryMode.IoTCentral && string.IsNullOrEmpty(this.IoTCentralToken))
                    {
                        throw new InvalidOperationException($"Device Registry Mode is set to {nameof(DeviceRegistryMode.IoTCentral)}, please set the {IoTCentralTokenKey} ConnectionString.");
                    }
                }

                internal override string RedisConnectionString => this.config.GetConnectionString(RedisConnectionStringKey);

                internal override string IoTHubConnectionString => this.config.GetConnectionString(IoTHubConnectionStringKey);

                internal override DeviceRegistryMode DeviceRegistryMode => this.config.GetValue(DeviceRegistryModeKey, DeviceRegistryMode.IoTHub);

                internal override string IoTCentralEndpoint => this.config.GetValue<string>(IoTCentralEndpointKey);

                internal override string IoTCentralDeviceProvisioningScopeId => this.config.GetValue<string>(IoTCentralDeviceProvisioningScopeIdKey);

                internal override string IoTCentralSASIoTDevicesPrimaryKey => this.config.GetConnectionString(IoTCentralSASIoTDevicesPrimaryKeyKey);

                internal override string IoTCentralSASIoTDevicesSecondaryKey => this.config.GetConnectionString(IoTCentralSASIoTDevicesSecondaryKeyKey);

                internal override string IoTCentralToken => this.config.GetConnectionString(IoTCentralTokenKey);
            }

            private class LocalConfigHandler : ConfigHandler
            {
                private readonly IConfiguration config;

                internal LocalConfigHandler()
                {
                    this.config = new ConfigurationBuilder()
                                    .SetBasePath(Environment.CurrentDirectory)
                                    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: false)
                                    .AddEnvironmentVariables()
                                    .Build();

                    if (this.DeviceRegistryMode == DeviceRegistryMode.IoTHub && string.IsNullOrEmpty(this.IoTHubConnectionString))
                    {
                        throw new InvalidOperationException($"Device Registry Mode is set to {nameof(DeviceRegistryMode.IoTHub)}, please set the {IoTHubConnectionStringKey} setting.");
                    }

                    if (this.DeviceRegistryMode == DeviceRegistryMode.IoTCentral && string.IsNullOrEmpty(this.IoTCentralToken))
                    {
                        throw new InvalidOperationException($"Device Registry Mode is set to {nameof(DeviceRegistryMode.IoTCentral)}, please set the {IoTCentralTokenKey} setting.");
                    }
                }

                internal override string RedisConnectionString => this.config.GetValue<string>(RedisConnectionStringKey);

                internal override string IoTHubConnectionString => this.config.GetValue<string>(IoTHubConnectionStringKey, string.Empty);

                internal override string IoTCentralToken => this.config.GetValue<string>(IoTCentralTokenKey, string.Empty);

                internal override DeviceRegistryMode DeviceRegistryMode => this.config.GetValue(DeviceRegistryModeKey, DeviceRegistryMode.IoTHub);

                internal override string IoTCentralEndpoint => this.config.GetValue<string>(IoTCentralEndpointKey, string.Empty);

                internal override string IoTCentralDeviceProvisioningScopeId => this.config.GetValue<string>(IoTCentralDeviceProvisioningScopeIdKey, string.Empty);

                internal override string IoTCentralSASIoTDevicesPrimaryKey => this.config.GetValue<string>(IoTCentralSASIoTDevicesPrimaryKeyKey);

                internal override string IoTCentralSASIoTDevicesSecondaryKey => this.config.GetValue<string>(IoTCentralSASIoTDevicesSecondaryKeyKey);
            }
        }
    }
}
