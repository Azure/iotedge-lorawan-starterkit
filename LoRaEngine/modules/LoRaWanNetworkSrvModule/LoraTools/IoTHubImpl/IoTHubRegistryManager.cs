// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.IoTHubImpl
{
    using System;
    using System.Globalization;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Logging;

    public sealed class IoTHubRegistryManager : IDeviceRegistryManager, IDisposable
    {
        private readonly RegistryManager instance;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger logger;

        public static IDeviceRegistryManager CreateWithProvider(
            Func<RegistryManager> registryManagerProvider,
            IHttpClientFactory httpClientFactory,
            ILogger logger)
        {
            return registryManagerProvider == null
                ? throw new ArgumentNullException(nameof(registryManagerProvider))
                : (IDeviceRegistryManager)new IoTHubRegistryManager(registryManagerProvider, httpClientFactory, logger);
        }

        internal IoTHubRegistryManager(Func<RegistryManager> registryManagerProvider, IHttpClientFactory httpClientFactory, ILogger logger)
        {
            this.instance = registryManagerProvider() ?? throw new InvalidOperationException("RegistryManager provider provided a null RegistryManager.");
            this.httpClientFactory = httpClientFactory;
            this.logger = logger;
        }

        public async Task<bool> AddDeviceAsync(IDeviceTwin twin)
        {
            var result = await this.instance.AddDeviceWithTwinAsync(new Device(twin?.DeviceId), twin.ToIoTHubDeviceTwin());

            if (result.IsSuccessful)
                return true;

            this.logger.LogWarning($"Failed to add Device with twin: \n{result.Errors}");

            return false;
        }

        public void Dispose() => this.instance?.Dispose();

        public async Task<string> GetDevicePrimaryKeyAsync(string deviceId)
        {
            var device = await this.instance.GetDeviceAsync(deviceId);

            return device?.Authentication?.SymmetricKey?.PrimaryKey;
        }

        public async Task<IDeviceTwin> UpdateTwinAsync(string deviceName, IDeviceTwin twin, string eTag)
            => new IoTHubDeviceTwin(await this.instance.UpdateTwinAsync(deviceName, twin.ToIoTHubDeviceTwin(), eTag));

        public async Task<IDeviceTwin> UpdateTwinAsync(string deviceId, string moduleId, IDeviceTwin deviceTwin, string eTag, CancellationToken cancellationToken)
            => new IoTHubDeviceTwin(await this.instance.UpdateTwinAsync(deviceId, moduleId, deviceTwin.ToIoTHubDeviceTwin(), eTag, cancellationToken));

        public Task RemoveDeviceAsync(string deviceId)
            => this.instance.RemoveDeviceAsync(deviceId);

        public IRegistryPageResult<IDeviceTwin> GetEdgeDevices()
        {
            var q = this.instance.CreateQuery($"SELECT * FROM devices.modules where moduleId = '{Constants.NetworkServerModuleId}'");
            return new IoTHubDeviceTwinPageResult(q);
        }

        public IRegistryPageResult<ILoRaDeviceTwin> GetAllLoRaDevices()
        {
            var q = this.instance.CreateQuery("SELECT * FROM devices WHERE is_defined(properties.desired.AppKey) OR is_defined(properties.desired.AppSKey) OR is_defined(properties.desired.NwkSKey)");
            return new IoTHubLoRaDeviceTwinPageResult(q);
        }

        public IRegistryPageResult<ILoRaDeviceTwin> GetLastUpdatedLoRaDevices(DateTime lastUpdateDateTime)
        {
            var formattedDateTime = lastUpdateDateTime.ToString(Constants.RoundTripDateTimeStringFormat, CultureInfo.InvariantCulture);
            var q = this.instance.CreateQuery($"SELECT * FROM devices where properties.desired.$metadata.$lastUpdated >= '{formattedDateTime}' OR properties.reported.$metadata.DevAddr.$lastUpdated >= '{formattedDateTime}'");
            return new IoTHubLoRaDeviceTwinPageResult(q);
        }

        public IRegistryPageResult<ILoRaDeviceTwin> FindLoRaDeviceByDevAddr(DevAddr someDevAddr)
        {
            var q = this.instance.CreateQuery($"SELECT * FROM devices WHERE properties.desired.DevAddr = '{someDevAddr}' OR properties.reported.DevAddr ='{someDevAddr}'", 100);
            return new IoTHubLoRaDeviceTwinPageResult(q);
        }

        public IRegistryPageResult<string> FindLnsByNetworkId(string networkId)
        {
            var q = this.instance.CreateQuery($"SELECT properties.desired.hostAddress, deviceId FROM devices.modules WHERE tags.network = '{networkId}'");
            return new JsonPageResult(q);
        }

        public IRegistryPageResult<ILoRaDeviceTwin> FindDeviceByDevEUI(DevEui devEUI)
        {
            var q = this.instance.CreateQuery($"SELECT * FROM devices WHERE deviceId = '{devEUI}'", 1);
            return new IoTHubLoRaDeviceTwinPageResult(q);
        }

        public async Task<ILoRaDeviceTwin> GetLoRaDeviceTwinAsync(string deviceId, CancellationToken? cancellationToken = null)
             => await this.instance.GetTwinAsync(deviceId, cancellationToken ?? CancellationToken.None) is { } twin ? new IoTHubLoRaDeviceTwin(twin) : null;

        public async Task<IDeviceTwin> GetTwinAsync(string deviceId, CancellationToken? cancellationToken = null)
             => await this.instance.GetTwinAsync(deviceId, cancellationToken ?? CancellationToken.None) is { } twin ? new IoTHubDeviceTwin(twin) : null;
        public async Task<IStationTwin> GetStationTwinAsync(StationEui stationEui, CancellationToken? cancellationToken = null)
            => await this.instance.GetTwinAsync(stationEui.ToString(), cancellationToken ?? CancellationToken.None) is { } twin ? new IoTHubStationTwin(twin) : null;
    }
}
