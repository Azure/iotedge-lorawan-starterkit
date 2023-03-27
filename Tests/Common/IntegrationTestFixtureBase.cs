// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.CommonAPI;
    using LoRaTools.IoTHubImpl;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    /// <summary>
    /// Integration test class.
    /// </summary>
    public abstract partial class IntegrationTestFixtureBase : IDisposable, IAsyncLifetime
    {
        /// <summary>
        /// expiry time for c2d.
        /// </summary>
        private const int C2dExpiryTime = 5;

        public const string MESSAGE_IDENTIFIER_PROPERTY_NAME = "messageIdentifier";
        private IDeviceRegistryManager registryManager;
        private TcpLogListener tcpLogListener;

        public TestConfiguration Configuration { get; }

        public EventHubDataCollector IoTHubMessages { get; private set; }

        private readonly Lazy<ServiceClient> serviceClient;
        private Microsoft.Azure.Devices.Client.ModuleClient moduleClient;

        protected IntegrationTestFixtureBase()
        {
            Configuration = TestConfiguration.GetConfiguration();
            this.serviceClient = new Lazy<ServiceClient>(() => ServiceClient.CreateFromConnectionString(Configuration.IoTHubConnectionString));
            TestLogger.Log($"[INFO] {nameof(Configuration.IoTHubAssertLevel)}: {Configuration.IoTHubAssertLevel}");
            TestLogger.Log($"[INFO] {nameof(Configuration.NetworkServerModuleLogAssertLevel)}: {Configuration.NetworkServerModuleLogAssertLevel}");

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        public string GetKey32(int deviceId, bool multiGw = false)
        {
            var target = multiGw ? Configuration.DeviceKeyFormatMultiGW : Configuration.DeviceKeyFormat;
            var format = string.IsNullOrEmpty(target) ? "00000000000000000000000000000000" : target;
            if (format.Length < 32)
            {
                format = format.PadLeft(32, '0');
            }

            return deviceId.ToString(format, CultureInfo.InvariantCulture);
        }

        public abstract void SetupTestDevices();

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine("Unhandled exception: " + e.ExceptionObject?.ToString() ?? string.Empty);
        }

        // Helper method to return all devices
        private IEnumerable<TestDeviceInfo> GetAllDevices()
        {
            var types = GetType();
            foreach (var prop in types.GetProperties())
            {
                if (prop.PropertyType == typeof(TestDeviceInfo))
                {
                    var device = (TestDeviceInfo)prop.GetValue(this);
                    yield return device;
                }
                else if (prop.PropertyType == typeof(IReadOnlyCollection<TestDeviceInfo>))
                {
                    var devices = (IReadOnlyCollection<TestDeviceInfo>)prop.GetValue(this);
                    foreach (var device in devices)
                    {
                        yield return device;
                    }
                }
            }
        }

        // Clear IoT Hub, TCP logs and Arduino serial logs
        public virtual void ClearLogs()
        {
            IoTHubMessages?.ResetEvents();
            this.tcpLogListener?.ResetEvents();
        }

        public async Task DisposeAsync()
        {
            if (IoTHubMessages is { } iotHubMessages)
            {
                try
                {
                    await iotHubMessages.StopAsync();
                }
                finally
                {
                    await iotHubMessages.DisposeAsync();
                }
            }
        }

        private IDeviceRegistryManager GetRegistryManager()
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            return this.registryManager ??= IoTHubRegistryManager.CreateWithProvider(() =>
                RegistryManager.CreateFromConnectionString(TestConfiguration.GetConfiguration().IoTHubConnectionString), new MockHttpClientFactory(), null);
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        public async Task<IDeviceTwin> GetTwinAsync(string deviceId)
        {
            return await GetRegistryManager().GetTwinAsync(deviceId);
        }

        public async Task SendCloudToDeviceMessageAsync(string deviceId, LoRaCloudToDeviceMessage message)
        {
            using var msg = new Message(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)))
            {
                ExpiryTimeUtc = DateTime.UtcNow.AddMinutes(C2dExpiryTime)
            };

            if (!string.IsNullOrEmpty(message.MessageId))
            {
                msg.MessageId = message.MessageId;
            }

            await SendCloudToDeviceMessageAsync(deviceId, msg);
        }

        public async Task CleanupC2DDeviceQueueAsync(string deviceId)
        {
            try
            {
                using (var client = Microsoft.Azure.Devices.Client.DeviceClient.CreateFromConnectionString(Configuration.IoTHubConnectionString + $";DeviceId={deviceId}", Microsoft.Azure.Devices.Client.TransportType.Amqp))
                {
                    Microsoft.Azure.Devices.Client.Message msg = null;
                    Console.WriteLine($"Cleaning up messages for device {deviceId}");
                    do
                    {
                        msg = await client.ReceiveAsync(TimeSpan.FromSeconds(10));
                        if (msg != null)
                        {
                            Console.WriteLine($"Found message to cleanup for device {deviceId}");
                            await client.CompleteAsync(msg);
                        }
                    }
                    while (msg != null);
                }

                Console.WriteLine($"Finished cleaning up messages for device {deviceId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Problem while cleaning up messages for device {deviceId}");
                Console.WriteLine(ex.ToString());
            }
        }

        public async Task SendCloudToDeviceMessageAsync(string deviceId, string messageId, string messageText, Dictionary<string, string> messageProperties = null)
        {
            using var msg = new Message(Encoding.UTF8.GetBytes(messageText));
            if (messageProperties != null)
            {
                foreach (var messageProperty in messageProperties)
                {
                    msg.Properties.Add(messageProperty.Key, messageProperty.Value);
                }
            }

            if (!string.IsNullOrEmpty(messageId))
            {
                msg.MessageId = messageId;
            }

            await SendCloudToDeviceMessageAsync(deviceId, msg);
        }

        private ServiceClient GetServiceClient() => this.serviceClient.Value;

        public async Task SendCloudToDeviceMessageAsync(string deviceId, Message message)
        {
            await GetServiceClient().SendAsync(deviceId, message);
        }

        /// <summary>
        /// Singleton for the module client
        /// Does not have to be thread-safe as CI does not run tests in parallel.
        /// </summary>
        public async Task<Microsoft.Azure.Devices.Client.ModuleClient> GetModuleClientAsync()
        {
            if (this.moduleClient == null)
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IOTEDGE_MODULEID")) &&
                    !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID")) &&
                    !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IOTEDGE_APIVERSION")))
                {
                    this.moduleClient = await Microsoft.Azure.Devices.Client.ModuleClient.CreateFromEnvironmentAsync();
                }
            }

            return this.moduleClient;
        }

        public async Task InvokeDeviceMethodAsync(string deviceId, string moduleId, CloudToDeviceMethod method)
        {
            using var sc = ServiceClient.CreateFromConnectionString(Configuration.IoTHubConnectionString);
            await sc.InvokeDeviceMethodAsync(deviceId, moduleId, method);
        }

        public async Task UpdateReportedTwinAsync(string deviceId, string twinName, int twinValue)
        {
            try
            {
                var device = Microsoft.Azure.Devices.Client.DeviceClient.CreateFromConnectionString(Configuration.IoTHubConnectionString, deviceId);
                var twinCollection = new TwinCollection();
                twinCollection[twinName] = twinValue;
                await device.UpdateReportedPropertiesAsync(twinCollection);
                await device.DisposeAsync();
            }
            catch (System.InvalidOperationException)
            {
            }
        }

        public virtual async Task InitializeAsync()
        {
            SetupTestDevices();

            // Fix device ID if a prefix was defined (DO NOT MOVE THIS LINE ABOVE DEVICE CREATION)
            foreach (var d in GetAllDevices())
            {
                if (!string.IsNullOrEmpty(Configuration.DevicePrefix))
                {
                    d.DeviceID = string.Concat(Configuration.DevicePrefix, d.DeviceID[Configuration.DevicePrefix.Length..]);
                    if (d.AppEui is { } someJoinEui)
                    {
                        d.AppEui = JoinEui.Parse($"{Configuration.DevicePrefix}{someJoinEui.ToString()[Configuration.DevicePrefix.Length..]}");
                    }

                    if (d.AppKey is { } someAppKey)
                    {
                        d.AppKey = AppKey.Parse(string.Concat(Configuration.DevicePrefix, someAppKey.ToString()[Configuration.DevicePrefix.Length..]));
                    }

                    if (d.AppSKey is { } someAppSessionKey)
                    {
                        d.AppSKey = AppSessionKey.Parse(string.Concat(Configuration.DevicePrefix, someAppSessionKey.ToString()[Configuration.DevicePrefix.Length..]));
                    }

                    if (d.NwkSKey is { } someNetworkSessionKey)
                    {
                        d.NwkSKey = NetworkSessionKey.Parse(string.Concat(Configuration.DevicePrefix, someNetworkSessionKey.ToString()[Configuration.DevicePrefix.Length..]));
                    }

                    if (d.DevAddr is { } someDevAddr)
                    {
                        d.DevAddr = DevAddr.Parse(Configuration.DevicePrefix + someDevAddr.ToString()[Configuration.DevicePrefix.Length..])
                                    with { NetworkId = checked((int)Configuration.NetId) };
                    }
                }
                else
                {
                    if (d.DevAddr is { } someDevAddr)
                    {
                        d.DevAddr = someDevAddr with { NetworkId = checked((int)Configuration.NetId) };
                    }
                }
            }

            if (Configuration.CreateDevices)
            {
                try
                {
                    await CreateOrUpdateDevicesAsync();
                }
                catch (Exception ex)
                {
                    TestLogger.Log($"[ERR] Failed to create devices in IoT Hub. {ex}");
                }
            }

            if (!string.IsNullOrEmpty(Configuration.IoTHubEventHubConnectionString) && Configuration.NetworkServerModuleLogAssertLevel != LogValidationAssertLevel.Ignore)
            {
                IoTHubMessages = new EventHubDataCollector(Configuration.IoTHubEventHubConnectionString, Configuration.IoTHubEventHubConsumerGroup);
                await IoTHubMessages.StartAsync();
            }

            if (Configuration.TcpLog)
            {
                this.tcpLogListener = TcpLogListener.Start(Configuration.TcpLogPort);
            }
        }

        public async Task UpdateExistingConcentratorThumbprint(StationEui stationEui, Func<string[], bool> condition, Action<List<string>> action)
        {
            TestLogger.Log($"Updating IoT Hub twin for concentrator {stationEui}...");
            var registryManager = GetRegistryManager();
            var stationDeviceId = GetDeviceId(stationEui);
            _ = await registryManager.GetTwinAsync(stationDeviceId) ?? throw new InvalidOperationException("Concentrator should exist in IoT Hub");
            var deviceTwin = await registryManager.GetTwinAsync(stationDeviceId);
            var initialClientThumbprints = JsonObject.Create(deviceTwin.Properties.Desired[BasicsStationConfigurationService.ClientThumbprintPropertyName]).AsArray();
            if (condition(initialClientThumbprints))
            {
                var arrayToList = new List<string>(initialClientThumbprints);
                action(arrayToList);
                deviceTwin.Properties.Desired[BasicsStationConfigurationService.ClientThumbprintPropertyName] = arrayToList.ToArray();
                await registryManager.UpdateTwinAsync(stationDeviceId, deviceTwin, deviceTwin.ETag);
            }
        }

        public async Task UpdateExistingConcentratorCrcValues(StationEui stationEui, uint crc)
        {
            TestLogger.Log($"Updating IoT Hub twin for concentrator {stationEui}...");
            var registryManager = GetRegistryManager();
            var stationDeviceId = GetDeviceId(stationEui);
            _ = await registryManager.GetTwinAsync(stationDeviceId) ?? throw new InvalidOperationException("Concentrator should exist in IoT Hub");
            var deviceTwin = await registryManager.GetTwinAsync(stationDeviceId);
            var cupsJson = ((object)deviceTwin.Properties.Desired[BasicsStationConfigurationService.CupsPropertyName]).ToString();
            var newCupsInfo = JsonSerializer.Deserialize<CupsTwinInfo>(cupsJson) with
            {
                TcCredCrc = crc,
                CupsCredCrc = crc,
            };
            deviceTwin.Properties.Desired[BasicsStationConfigurationService.CupsPropertyName] = JsonSerializer.SerializeToElement(newCupsInfo);
            await registryManager.UpdateTwinAsync(stationDeviceId, deviceTwin, deviceTwin.ETag);
        }

        public async Task UpdateExistingFirmwareUpgradeValues(StationEui stationEui, uint crc, string digestBase64String, string package, Uri fwUrl)
        {
            TestLogger.Log($"Updating IoT Hub twin for fw upgrades of concentrator {stationEui}...");
            var registryManager = GetRegistryManager();
            var stationDeviceId = GetDeviceId(stationEui);
            _ = await registryManager.GetTwinAsync(stationDeviceId) ?? throw new InvalidOperationException("Concentrator should exist in IoT Hub");
            var deviceTwin = await registryManager.GetTwinAsync(stationDeviceId);
            var cupsJson = ((object)deviceTwin.Properties.Desired[BasicsStationConfigurationService.CupsPropertyName]).ToString();
            var newCupsInfo = JsonSerializer.Deserialize<CupsTwinInfo>(cupsJson) with
            {
                FwKeyChecksum = crc,
                FwSignatureInBase64 = digestBase64String,
                Package = package,
                FwUrl = new Uri(fwUrl.GetLeftPart(UriPartial.Path)) // the GetLeftPart is useful to exclude any potential SAS token stored in the original variable
            };
            deviceTwin.Properties.Desired[BasicsStationConfigurationService.CupsPropertyName] = JsonSerializer.SerializeToElement(newCupsInfo);
            await registryManager.UpdateTwinAsync(stationDeviceId, deviceTwin, deviceTwin.ETag);
        }

        private static string GetDeviceId(StationEui eui) => eui.ToString();

        private async Task CreateOrUpdateDevicesAsync()
        {
            TestLogger.Log($"Creating or updating IoT Hub devices...");
            var registryManager = GetRegistryManager();
            foreach (var testDevice in GetAllDevices().Where(x => x.IsIoTHubDevice))
            {
                var deviceID = testDevice.DeviceID;
                if (!string.IsNullOrEmpty(Configuration.DevicePrefix))
                {
                    deviceID = string.Concat(Configuration.DevicePrefix, deviceID[Configuration.DevicePrefix.Length..]);
                    testDevice.DeviceID = deviceID;
                }

                var getDeviceResult = await registryManager.GetTwinAsync(testDevice.DeviceID);
                if (getDeviceResult == null)
                {
                    TestLogger.Log($"Device {testDevice.DeviceID} does not exist. Creating");
                    var twin = new Twin(testDevice.DeviceID);
                    twin.Properties.Desired = new TwinCollection(JsonSerializer.Serialize(testDevice.GetDesiredProperties()));

                    TestLogger.Log($"Creating device {testDevice.DeviceID}");
                    await registryManager.AddDeviceAsync(new IoTHubDeviceTwin(twin));
                }
                else
                {
                    // compare device twin and make changes if needed
                    var deviceTwin = await registryManager.GetTwinAsync(testDevice.DeviceID);
                    var twinCollectionReader = new TwinCollectionReader(deviceTwin.Properties.Desired, NullLogger.Instance);
                    var desiredProperties = testDevice.GetDesiredProperties();
                    foreach (var kv in desiredProperties)
                    {
                        if (kv.Key == BasicsStationConfigurationService.RouterConfigPropertyName && deviceTwin.Properties.Desired.Contains(kv.Key))
                        {
                            // The router config property cannot be updated automatically. If it is present, we assume that it is correct.
                            continue;
                        }

                        if (twinCollectionReader.SafeRead<string>(kv.Key) != kv.Value.ToString())
                        {
                            var existingValue = string.Empty;
                            if (deviceTwin.Properties.Desired.Contains(kv.Key))
                            {
                                existingValue = deviceTwin.Properties.Desired[kv.Key].ToString();
                            }

                            TestLogger.Log($"Unexpected value for device {testDevice.DeviceID} twin property {kv.Key}, expecting '{kv.Value}', actual is '{existingValue}'");

                            var patch = new Twin();
                            patch.Properties.Desired = new TwinCollection(JsonSerializer.Serialize(desiredProperties));
                            await registryManager.UpdateTwinAsync(testDevice.DeviceID, new IoTHubDeviceTwin(patch), deviceTwin.ETag);
                            TestLogger.Log($"Update twin for device {testDevice.DeviceID}");
                            break;
                        }
                    }
                }
            }

            TestLogger.Log($"Done creating or updating IoT Hub devices.");
        }

        // Helper method to return TestDeviceInfo by a property name, NOT THE DEVICE ID!!
        // Usefull when running theories
        public TestDeviceInfo GetDeviceByPropertyName(string propertyName)
        {
            return (TestDeviceInfo)GetType().GetProperty(propertyName).GetValue(this);
        }

        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            TestLogger.Log($"{nameof(IntegrationTestFixtureBase)} disposed");

            if (!this.disposedValue)
            {
                if (disposing)
                {
                    AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;

                    IoTHubMessages = null;

                    this.tcpLogListener?.Dispose();
                    this.tcpLogListener = null;
                }

                this.disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
