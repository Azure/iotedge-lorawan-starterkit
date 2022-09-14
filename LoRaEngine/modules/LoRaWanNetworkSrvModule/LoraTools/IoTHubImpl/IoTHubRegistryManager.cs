// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.IoTHubImpl
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

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

        public async Task DeployEdgeDeviceAsync(
                string deviceId,
                string resetPin,
                string spiSpeed,
                string spiDev,
                string publishingUserName,
                string publishingPassword,
                string networkId = Constants.NetworkId,
                string lnsHostAddress = "ws://mylns:5000")
        {
            // Get function facade key
            var base64Auth = Convert.ToBase64String(Encoding.Default.GetBytes($"{publishingUserName}:{publishingPassword}"));
            var apiUrl = new Uri($"https://{Environment.GetEnvironmentVariable("WEBSITE_CONTENTSHARE")}.scm.azurewebsites.net");
            var siteUrl = new Uri($"https://{Environment.GetEnvironmentVariable("WEBSITE_CONTENTSHARE")}.azurewebsites.net");
            string jwt;
            using (var client = this.httpClientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Basic {base64Auth}");
                var result = await client.GetAsync(new Uri(apiUrl, "/api/functions/admin/token"));
                jwt = (await result.Content.ReadAsStringAsync()).Trim('"'); // get  JWT for call funtion key
            }

            var facadeKey = string.Empty;
            using (var client = this.httpClientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + jwt);
                var response = await client.GetAsync(new Uri(siteUrl, "/admin/host/keys"));
                var jsonResult = await response.Content.ReadAsStringAsync();
                dynamic resObject = JsonConvert.DeserializeObject(jsonResult);
                facadeKey = resObject.keys[0].value;
            }

            var edgeGatewayDevice = new Device(deviceId)
            {
                Capabilities = new DeviceCapabilities()
                {
                    IotEdge = true
                }
            };

            _ = await this.instance.AddDeviceAsync(edgeGatewayDevice);
            _ = await this.instance.AddModuleAsync(new Module(deviceId, "LoRaWanNetworkSrvModule"));

            async Task<ConfigurationContent> GetConfigurationContentAsync(Uri configLocation, IDictionary<string, string> tokenReplacements)
            {
                using var httpClient = this.httpClientFactory.CreateClient();
                var json = await httpClient.GetStringAsync(configLocation);
                foreach (var r in tokenReplacements)
                    json = json.Replace(r.Key, r.Value, StringComparison.Ordinal);
                return JsonConvert.DeserializeObject<ConfigurationContent>(json);
            }

            var deviceConfigurationContent = await GetConfigurationContentAsync(new Uri(Environment.GetEnvironmentVariable("DEVICE_CONFIG_LOCATION")), new Dictionary<string, string>
            {
                ["[$reset_pin]"] = resetPin,
                ["[$spi_speed]"] = string.IsNullOrEmpty(spiSpeed) || string.Equals(spiSpeed, "8", StringComparison.OrdinalIgnoreCase) ? string.Empty : ",'SPI_SPEED':{'value':'2'}",
                ["[$spi_dev]"] = string.IsNullOrEmpty(spiDev) || string.Equals(spiDev, "0", StringComparison.OrdinalIgnoreCase) ? string.Empty : $",'SPI_DEV':{{'value':'{spiDev}'}}"
            });

            await this.instance.ApplyConfigurationContentOnDeviceAsync(deviceId, deviceConfigurationContent);

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOG_ANALYTICS_WORKSPACE_ID")))
            {
                this.logger.LogDebug("Opted-in to use Azure Monitor on the edge. Deploying the observability layer.");
                // If Appinsights Key is set this means that user opted in to use Azure Monitor.
                _ = await this.instance.AddModuleAsync(new Module(deviceId, "IotHubMetricsCollectorModule"));
                var observabilityConfigurationContent = await GetConfigurationContentAsync(new Uri(Environment.GetEnvironmentVariable("OBSERVABILITY_CONFIG_LOCATION")), new Dictionary<string, string>
                {
                    ["[$iot_hub_resource_id]"] = Environment.GetEnvironmentVariable("IOT_HUB_RESOURCE_ID"),
                    ["[$log_analytics_workspace_id]"] = Environment.GetEnvironmentVariable("LOG_ANALYTICS_WORKSPACE_ID"),
                    ["[$log_analytics_shared_key]"] = Environment.GetEnvironmentVariable("LOG_ANALYTICS_WORKSPACE_KEY")
                });

                _ = await this.instance.AddConfigurationAsync(new Configuration($"obs-{Guid.NewGuid()}")
                {
                    Content = observabilityConfigurationContent,
                    TargetCondition = $"deviceId='{deviceId}'"
                });
            }

            var twin = new Twin();
            twin.Properties.Desired = new TwinCollection($"{{FacadeServerUrl:'https://{Environment.GetEnvironmentVariable("FACADE_HOST_NAME", EnvironmentVariableTarget.Process)}.azurewebsites.net/api/',FacadeAuthCode: '{facadeKey}'}}");
            twin.Properties.Desired["hostAddress"] = new Uri(lnsHostAddress);
            twin.Tags[Constants.NetworkTagName] = networkId;
            var remoteTwin = await this.instance.GetTwinAsync(deviceId);

            _ = await this.instance.UpdateTwinAsync(deviceId, "LoRaWanNetworkSrvModule", twin, remoteTwin.ETag);
        }

        public async Task DeployConcentratorAsync(string stationEuiString, string region, string networkId = Constants.NetworkId)
        {
            // Deploy concentrator
            using var httpClient = this.httpClientFactory.CreateClient();
            var regionalConfiguration = region switch
            {
                var s when string.Equals("EU", s, StringComparison.OrdinalIgnoreCase) => await httpClient.GetStringAsync(new Uri(Environment.GetEnvironmentVariable("EU863_CONFIG_LOCATION", EnvironmentVariableTarget.Process))),
                var s when string.Equals("US", s, StringComparison.OrdinalIgnoreCase) => await httpClient.GetStringAsync(new Uri(Environment.GetEnvironmentVariable("US902_CONFIG_LOCATION", EnvironmentVariableTarget.Process))),
                _ => throw new SwitchExpressionException("Region should be either 'EU' or 'US'")
            };

            var concentratorDevice = new Device(stationEuiString);
            _ = await this.instance.AddDeviceAsync(concentratorDevice);
            var concentratorTwin = await this.instance.GetTwinAsync(stationEuiString);
            concentratorTwin.Properties.Desired["routerConfig"] = JsonConvert.DeserializeObject<JObject>(regionalConfiguration);
            concentratorTwin.Tags[Constants.NetworkTagName] = networkId;
            _ = await this.instance.UpdateTwinAsync(stationEuiString, concentratorTwin, concentratorTwin.ETag);
        }

        public async Task<bool> DeployEndDevicesAsync()
        {
            var otaaDevice = await this.instance.GetDeviceAsync(Constants.OtaaDeviceId)
                                ?? await this.instance.AddDeviceAsync(new Device(Constants.OtaaDeviceId));

            var otaaEndTwin = new Twin();
            otaaEndTwin.Properties.Desired = new TwinCollection(/*lang=json*/ @"{AppEUI:'BE7A0000000014E2',AppKey:'8AFE71A145B253E49C3031AD068277A1',GatewayID:'',SensorDecoder:'DecoderValueSensor'}");
            var otaaRemoteTwin = _ = await this.instance.GetTwinAsync(Constants.OtaaDeviceId);
            _ = await this.instance.UpdateTwinAsync(Constants.OtaaDeviceId, otaaEndTwin, otaaRemoteTwin.ETag);

            var abpDevice = await this.instance.GetDeviceAsync(Constants.AbpDeviceId)
                                ?? await this.instance.AddDeviceAsync(new Device(Constants.AbpDeviceId));
            var abpTwin = new Twin();
            abpTwin.Properties.Desired = new TwinCollection(/*lang=json*/ @"{AppSKey:'2B7E151628AED2A6ABF7158809CF4F3C',NwkSKey:'3B7E151628AED2A6ABF7158809CF4F3C',GatewayID:'',DevAddr:'0228B1B1',SensorDecoder:'DecoderValueSensor'}");
            var abpRemoteTwin = await this.instance.GetTwinAsync(Constants.AbpDeviceId);
            _ = await this.instance.UpdateTwinAsync(Constants.AbpDeviceId, abpTwin, abpRemoteTwin.ETag);

            return abpDevice != null && otaaDevice != null;
        }

        public Task AddModuleAsync(string deviceId, string moduleId)
            => this.instance.AddModuleAsync(new Module(deviceId, moduleId));
    }
}
