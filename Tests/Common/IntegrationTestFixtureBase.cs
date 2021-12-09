// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
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
        private RegistryManager registryManager;
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

        public Task DisposeAsync() => Task.FromResult(0);

        private RegistryManager GetRegistryManager()
        {
            return this.registryManager ??= RegistryManager.CreateFromConnectionString(Configuration.IoTHubConnectionString);
        }

        public async Task<Twin> GetTwinAsync(string deviceId)
        {
            return await GetRegistryManager().GetTwinAsync(deviceId);
        }

        public async Task SendCloudToDeviceMessageAsync(string deviceId, LoRaCloudToDeviceMessage message)
        {
            using var msg = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)))
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

        public async Task InvokeModuleDirectMethodAsync(string edgeDeviceId, string moduleId, string methodName, object body)
        {
            try
            {
                var c2d = new CloudToDeviceMethod(methodName, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
                c2d.SetPayloadJson(JsonConvert.SerializeObject(body));
                await GetServiceClient().InvokeDeviceMethodAsync(edgeDeviceId, moduleId, c2d);
            }
            catch (Exception ex)
            {
                TestLogger.Log($"[ERROR] Failed to call direct method, deviceId: {edgeDeviceId}, moduleId: {moduleId}, method: {methodName}: {ex.Message}");
                throw;
            }
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
                    if (!string.IsNullOrEmpty(d.AppEUI))
                    {
                        d.AppEUI = string.Concat(Configuration.DevicePrefix, d.AppEUI[Configuration.DevicePrefix.Length..]);
                    }

                    if (!string.IsNullOrEmpty(d.AppKey))
                    {
                        d.AppKey = string.Concat(Configuration.DevicePrefix, d.AppKey[Configuration.DevicePrefix.Length..]);
                    }

                    if (!string.IsNullOrEmpty(d.AppSKey))
                    {
                        d.AppSKey = string.Concat(Configuration.DevicePrefix, d.AppSKey[Configuration.DevicePrefix.Length..]);
                    }

                    if (!string.IsNullOrEmpty(d.NwkSKey))
                    {
                        d.NwkSKey = string.Concat(Configuration.DevicePrefix, d.NwkSKey[Configuration.DevicePrefix.Length..]);
                    }

                    if (!string.IsNullOrEmpty(d.DevAddr))
                    {
                        d.DevAddr = NetIdHelper.SetNwkIdPart(string.Concat(Configuration.DevicePrefix, d.DevAddr[Configuration.DevicePrefix.Length..]), Configuration.NetId);
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(d.DevAddr))
                    {
                        d.DevAddr = NetIdHelper.SetNwkIdPart(d.DevAddr, Configuration.NetId);
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

                var getDeviceResult = await registryManager.GetDeviceAsync(testDevice.DeviceID);
                if (getDeviceResult == null)
                {
                    TestLogger.Log($"Device {testDevice.DeviceID} does not exist. Creating");
                    var device = new Device(testDevice.DeviceID);
                    var twin = new Twin(testDevice.DeviceID);
                    twin.Properties.Desired = new TwinCollection(JsonConvert.SerializeObject(testDevice.GetDesiredProperties()));

                    TestLogger.Log($"Creating device {testDevice.DeviceID}");
                    await registryManager.AddDeviceWithTwinAsync(device, twin);
                }
                else
                {
                    // compare device twin and make changes if needed
                    var deviceTwin = await registryManager.GetTwinAsync(testDevice.DeviceID);
                    var desiredProperties = testDevice.GetDesiredProperties();
                    foreach (var kv in desiredProperties)
                    {
                        if (!deviceTwin.Properties.Desired.Contains(kv.Key) || (string)deviceTwin.Properties.Desired[kv.Key] != kv.Value.ToString())
                        {
                            var existingValue = string.Empty;
                            if (deviceTwin.Properties.Desired.Contains(kv.Key))
                            {
                                existingValue = deviceTwin.Properties.Desired[kv.Key].ToString();
                            }

                            TestLogger.Log($"Unexpected value for device {testDevice.DeviceID} twin property {kv.Key}, expecting '{kv.Value}', actual is '{existingValue}'");

                            var patch = new Twin();
                            patch.Properties.Desired = new TwinCollection(JsonConvert.SerializeObject(desiredProperties));
                            await registryManager.UpdateTwinAsync(testDevice.DeviceID, patch, deviceTwin.ETag);
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

                    IoTHubMessages?.Dispose();
                    IoTHubMessages = null;
                    this.registryManager?.Dispose();
                    this.registryManager = null;

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
