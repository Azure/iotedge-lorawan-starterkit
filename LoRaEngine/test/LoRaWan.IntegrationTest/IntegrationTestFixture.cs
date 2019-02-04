// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.EventHubs;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public partial class IntegrationTestFixture : IDisposable, IAsyncLifetime
    {
        public const string MESSAGE_IDENTIFIER_PROPERTY_NAME = "messageIdentifier";

        RegistryManager registryManager;
        private UdpLogListener udpLogListener;

        public TestConfiguration Configuration { get; }

        public EventHubDataCollector IoTHubMessages { get; private set; }

        // Device1_OTAA: used for join test only
        public TestDeviceInfo Device1_OTAA { get; private set; }

        // Device2_OTAA: used for failed join (wrong devEUI)
        public TestDeviceInfo Device2_OTAA { get; private set; }

        // Device3_OTAA: used for failed join (wrong appKey)
        public TestDeviceInfo Device3_OTAA { get; private set; }

        // Device4_OTAA: used for OTAA confirmed & unconfirmed messaging
        public TestDeviceInfo Device4_OTAA { get; private set; }

        // Device5_ABP: used for ABP confirmed & unconfirmed messaging
        public TestDeviceInfo Device5_ABP { get; private set; }

        // Device6_ABP: used for ABP wrong devaddr
        public TestDeviceInfo Device6_ABP { get; private set; }

        // Device7_ABP: used for ABP wrong nwkskey
        public TestDeviceInfo Device7_ABP { get; private set; }

        // Device8_ABP: used for ABP invalid nwkskey (mic fails)
        public TestDeviceInfo Device8_ABP { get; private set; }

        // Device9_OTAA: used for OTAA confirmed messages, C2D test
        public TestDeviceInfo Device9_OTAA { get; private set; }

        // Device10_OTAA: used for OTAA unconfirmed messages, C2D test
        public TestDeviceInfo Device10_OTAA { get; private set; }

        // Device11_OTAA: used for http decoder
        public TestDeviceInfo Device11_OTAA { get; private set; }

        // Device12_OTAA: used for reflection based decoder
        public TestDeviceInfo Device12_OTAA { get; private set; }

        // Device13_OTAA: used for wrong AppEUI OTAA join
        public TestDeviceInfo Device13_OTAA { get; private set; }

        // Device14_OTAA: used for test confirmed C2D
        public TestDeviceInfo Device14_OTAA { get; private set; }

        // Device15_OTAA: used for test fport C2D
        public TestDeviceInfo Device15_OTAA { get; private set; }

        // Device16_ABP: used for test on multiple device with same devaddr
        public TestDeviceInfo Device16_ABP { get; private set; }

        // Device17_ABP: used for test on multiple device with same devaddr
        public TestDeviceInfo Device17_ABP { get; private set; }

        // Device18_ABP: used for C2D invalid fport testing
        public TestDeviceInfo Device18_ABP { get; private set; }

        // Device19_ABP: used for C2D invalid fport testing
        public TestDeviceInfo Device19_ABP { get; private set; }

        // Device20_OTAA: used for OTAA confirmed & unconfirmed messaging
        public TestDeviceInfo Device20_OTAA { get; private set; }

        // Device21_ABP: Preferred 2nd window
        public TestDeviceInfo Device21_ABP { get; private set; }

        // Device1001_Simulated_ABP: used for ABP simulator
        public TestDeviceInfo Device1001_Simulated_ABP { get; private set; }

        // Device1002_Simulated_OTAA: used for simulator
        public TestDeviceInfo Device1002_Simulated_OTAA { get; private set; }

        // Device1003_Simulated_HttpBasedDecoder: used for simulator http based decoding test
        public TestDeviceInfo Device1003_Simulated_HttpBasedDecoder { get; private set; }

        List<TestDeviceInfo> deviceRange1000_ABP = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange1000_ABP { get { return this.deviceRange1000_ABP; } }

        List<TestDeviceInfo> deviceRange1200_100_ABP = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange1200_100_ABP { get { return this.deviceRange1200_100_ABP; } }

        List<TestDeviceInfo> deviceRange1300_10_OTAA = new List<TestDeviceInfo>();

        public IReadOnlyCollection<TestDeviceInfo> DeviceRange1300_10_OTAA { get { return this.deviceRange1300_10_OTAA; } }

        public IntegrationTestFixture()
        {
            this.Configuration = TestConfiguration.GetConfiguration();
            TestLogger.Log($"[INFO] {nameof(this.Configuration.IoTHubAssertLevel)}: {this.Configuration.IoTHubAssertLevel}");
            TestLogger.Log($"[INFO] {nameof(this.Configuration.NetworkServerModuleLogAssertLevel)}: {this.Configuration.NetworkServerModuleLogAssertLevel}");

            AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;

            this.SetupTestDevices();

            // Fix device ID if a prefix was defined (DO NOT MOVE THIS LINE ABOVE DEVICE CREATION)
            foreach (var d in this.GetAllDevices())
            {
                if (!string.IsNullOrEmpty(this.Configuration.DevicePrefix))
                {

                    d.DeviceID = string.Concat(this.Configuration.DevicePrefix, d.DeviceID.Substring(this.Configuration.DevicePrefix.Length, d.DeviceID.Length - this.Configuration.DevicePrefix.Length));
                    if (!string.IsNullOrEmpty(d.AppEUI))
                    {
                        d.AppEUI = string.Concat(this.Configuration.DevicePrefix, d.AppEUI.Substring(this.Configuration.DevicePrefix.Length, d.AppEUI.Length - this.Configuration.DevicePrefix.Length));
                    }

                    if (!string.IsNullOrEmpty(d.AppKey))
                    {
                        d.AppKey = string.Concat(this.Configuration.DevicePrefix, d.AppKey.Substring(this.Configuration.DevicePrefix.Length, d.AppKey.Length - this.Configuration.DevicePrefix.Length));
                    }

                    if (!string.IsNullOrEmpty(d.AppSKey))
                    {
                        d.AppSKey = string.Concat(this.Configuration.DevicePrefix, d.AppSKey.Substring(this.Configuration.DevicePrefix.Length, d.AppSKey.Length - this.Configuration.DevicePrefix.Length));
                    }

                    if (!string.IsNullOrEmpty(d.NwkSKey))
                    {
                        d.NwkSKey = string.Concat(this.Configuration.DevicePrefix, d.NwkSKey.Substring(this.Configuration.DevicePrefix.Length, d.NwkSKey.Length - this.Configuration.DevicePrefix.Length));
                    }

                    if (!string.IsNullOrEmpty(d.DevAddr))
                    {
                        d.DevAddr = LoRaTools.Utils.NetIdHelper.SetNwkIdPart(string.Concat(this.Configuration.DevicePrefix, d.DevAddr.Substring(this.Configuration.DevicePrefix.Length, d.DevAddr.Length - this.Configuration.DevicePrefix.Length)), this.Configuration.NetId);
                    }
                }
                else
                {
                    d.DevAddr = LoRaTools.Utils.NetIdHelper.SetNwkIdPart(d.DevAddr, this.Configuration.NetId);
                }
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine("Unhandled exception: " + e.ExceptionObject?.ToString() ?? string.Empty);
        }

        // Setup the test devices here
        void SetupTestDevices()
        {
            var gatewayID = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID") ?? this.Configuration.LeafDeviceGatewayID;

            // Device1_OTAA: used for join test only
            this.Device1_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000001",
                AppEUI = "0000000000000001",
                AppKey = "00000000000000000000000000000001",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device2_OTAA: used for failed join (wrong devEUI)
            this.Device2_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000002",
                AppEUI = "0000000000000002",
                AppKey = "00000000000000000000000000000002",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = false,
            };

            // Device3_OTAA: used for failed join (wrong appKey)
            this.Device3_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000003",
                AppEUI = "0000000000000003",
                AppKey = "00000000000000000000000000000003",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
            };

            // Device4_OTAA: used for OTAA confirmed & unconfirmed messaging
            this.Device4_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000004",
                AppEUI = "0000000000000004",
                AppKey = "00000000000000000000000000000004",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
            };

            // Device5_ABP: used for ABP confirmed & unconfirmed messaging
            this.Device5_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000005",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = "00000000000000000000000000000005",
                NwkSKey = "00000000000000000000000000000005",
                DevAddr = "0028B1B0",
            };

            // Device6_ABP: used for ABP wrong devaddr
            this.Device6_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000006",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = false,
                AppSKey = "00000000000000000000000000000006",
                NwkSKey = "00000000000000000000000000000006",
                DevAddr = "00000006",
            };

            // Device7_ABP: used for ABP wrong nwkskey
            this.Device7_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000007",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = "00000000000000000000000000000007",
                NwkSKey = "00000000000000000000000000000007",
                DevAddr = "00000007",
            };

            // Device8_ABP: used for ABP invalid nwkskey (mic fails)
            this.Device8_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000008",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = "00000000000000000000000000000008",
                NwkSKey = "00000000000000000000000000000008",
                DevAddr = "00000008",
            };

            // Device9_OTAA: used for confirmed message & C2D
            this.Device9_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000009",
                AppEUI = "0000000000000009",
                AppKey = "00000000000000000000000000000009",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device10_OTAA: used for unconfirmed message & C2D
            this.Device10_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000010",
                AppEUI = "0000000000000010",
                AppKey = "00000000000000000000000000000010",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
            };

            // Device11_OTAA: used for http decoder
            this.Device11_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000011",
                AppEUI = "0000000000000011",
                AppKey = "00000000000000000000000000000011",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "http://sensordecodermodule/api/DecoderValueSensor",
            };

            // Device12_OTAA: used for reflection based decoder
            this.Device12_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000012",
                AppEUI = "0000000000000012",
                AppKey = "00000000000000000000000000000012",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            // Device13_OTAA: used for Join with wrong AppEUI
            this.Device13_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000013",
                AppEUI = "0000000000000013",
                AppKey = "00000000000000000000000000000013",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            // Device14_OTAA: used for Confirmed C2D message
            this.Device14_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000014",
                AppEUI = "0000000000000014",
                AppKey = "00000000000000000000000000000014",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            // Device15_OTAA: used for the Fport test
            this.Device15_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000015",
                AppEUI = "0000000000000015",
                AppKey = "00000000000000000000000000000015",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };
            // Device16_ABP: used for same DevAddr test
            this.Device16_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000016",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = "00000000000000000000000000000016",
                NwkSKey = "00000000000000000000000000000016",
                DevAddr = "00000016",
            };

            // Device17_ABP: used for same DevAddr test
            this.Device17_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000017",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = "00000000000000000000000000000017",
                NwkSKey = "00000000000000000000000000000017",
                DevAddr = "00000017",
            };

            // Device18_ABP: used for C2D invalid fport testing
            this.Device18_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000018",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
                AppSKey = "00000000000000000000000000000018",
                NwkSKey = "00000000000000000000000000000018",
                DevAddr = "00000018",
            };

            // Device19_ABP: used for C2D invalid fport testing
            this.Device19_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000019",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
                AppSKey = "00000000000000000000000000000019",
                NwkSKey = "00000000000000000000000000000019",
                DevAddr = "00000019",
            };

            // Device20_OTAA: used for join and rejoin test
            this.Device20_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000020",
                AppEUI = "0000000000000020",
                AppKey = "00000000000000000000000000000020",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            // Device21_ABP: Preferred 2nd window
            this.Device21_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000021",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
                AppSKey = "00000000000000000000000000000021",
                NwkSKey = "00000000000000000000000000000021",
                DevAddr = "00000021",
                PreferredWindow = 2,
            };


            // Simulated devices start at 1000

            // Device1001_Simulated_ABP: used for ABP simulator
            this.Device1001_Simulated_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000001001",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey = "00000000000000000000000000001001",
                NwkSKey = "00000000000000000000000000001001",
                DevAddr = "00001001",
            };

            // Device1002_Simulated_OTAA: used for simulator
            this.Device1002_Simulated_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000001002",
                AppEUI = "0000000000001002",
                AppKey = "00000000000000000000000000001002",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "DecoderValueSensor",
            };

            // Device1003_Simulated_HttpBasedDecoder: used for simulator http based decoding test
            this.Device1003_Simulated_HttpBasedDecoder = new TestDeviceInfo
            {
                DeviceID = "0000000000001003",
                AppEUI = "0000000000001003",
                AppKey = "00000000000000000000000000001003",
                GatewayID = gatewayID,
                IsIoTHubDevice = true,
                SensorDecoder = "http://localhost:8888/api/DecoderValueSensor",
            };

            for (var deviceID = 1100; deviceID <= 1110; deviceID++)
            {
                this.deviceRange1000_ABP.Add(
                    new TestDeviceInfo
                    {
                        DeviceID = deviceID.ToString("0000000000000000"),
                        AppEUI = deviceID.ToString("0000000000000000"),
                        AppKey = deviceID.ToString("00000000000000000000000000000000"),
                        GatewayID = gatewayID,
                        IsIoTHubDevice = true,
                        SensorDecoder = "DecoderValueSensor",
                        AppSKey = deviceID.ToString("00000000000000000000000000000000"),
                        NwkSKey = deviceID.ToString("00000000000000000000000000000000"),
                        DevAddr = deviceID.ToString("00000000"),
                    }
                );
            }

            // Range of 100 ABP devices from 1200 to 1299: Used for load testing
            for (var deviceID = 1200; deviceID <= 1299; deviceID++)
            {
                this.deviceRange1200_100_ABP.Add(
                    new TestDeviceInfo
                    {
                        DeviceID = deviceID.ToString("0000000000000000"),
                        GatewayID = gatewayID,
                        IsIoTHubDevice = true,
                        SensorDecoder = "DecoderValueSensor",
                        AppSKey = deviceID.ToString("00000000000000000000000000000000"),
                        NwkSKey = deviceID.ToString("00000000000000000000000000000000"),
                        DevAddr = deviceID.ToString("00000000"),
                    }
                );
            }

            // Range of 10 OTAA devices from 1300 to 1309: Used for load testing
            for (var deviceID = 1300; deviceID <= 1309; deviceID++)
            {
                this.deviceRange1300_10_OTAA.Add(
                    new TestDeviceInfo
                    {
                        DeviceID = deviceID.ToString("0000000000000000"),
                        AppEUI = deviceID.ToString("0000000000000000"),
                        AppKey = deviceID.ToString("00000000000000000000000000000000"),
                        GatewayID = gatewayID,
                        IsIoTHubDevice = true,
                        SensorDecoder = "DecoderValueSensor",
                    }
                );
            }
        }

        // Helper method to return all devices
        IEnumerable<TestDeviceInfo> GetAllDevices()
        {
            var t = this.GetType();
            foreach (var prop in t.GetProperties())
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

        // Clear IoT Hub, Udp logs and Arduino serial logs
        public void ClearLogs()
        {
            this.IoTHubMessages?.ResetEvents();
            this.udpLogListener?.ResetEvents();
            this.ArduinoDevice?.ClearSerialLogs();
        }

        bool disposed = false;

        private LoRaArduinoSerial arduinoDevice;

        public LoRaArduinoSerial ArduinoDevice { get { return this.arduinoDevice; } }

        public void Dispose()
        {
            TestLogger.Log($"{nameof(IntegrationTestFixture)} disposed");

            if (this.disposed)
            {
                return;
            }

            AppDomain.CurrentDomain.UnhandledException -= this.OnUnhandledException;

            this.arduinoDevice?.Dispose();
            this.arduinoDevice = null;

            this.IoTHubMessages?.Dispose();
            this.IoTHubMessages = null;
            this.registryManager?.Dispose();
            this.registryManager = null;

            this.udpLogListener?.Dispose();
            this.udpLogListener = null;

            GC.SuppressFinalize(this);

            this.disposed = true;
        }

        public Task DisposeAsync() => Task.FromResult(0);

        RegistryManager GetRegistryManager()
        {
            return (this.registryManager ?? (this.registryManager = RegistryManager.CreateFromConnectionString(this.Configuration.IoTHubConnectionString)));

        }

        internal async Task<Twin> GetTwinAsync(string deviceId)
        {
            return await this.GetRegistryManager().GetTwinAsync(deviceId);
        }

        internal Task SendCloudToDeviceMessage(string deviceId, string messageText, Dictionary<String, String> messageProperties = null) => this.SendCloudToDeviceMessage(deviceId, null, messageText, messageProperties);

        internal async Task SendCloudToDeviceMessage(string deviceId, string messageId, string messageText, Dictionary<String, String> messageProperties = null)
        {
            var msg = new Message(Encoding.UTF8.GetBytes(messageText));
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

            await this.SendCloudToDeviceMessage(deviceId, msg);
        }

        internal async Task SendCloudToDeviceMessage(string deviceId, Message message)
        {
            ServiceClient sc = ServiceClient.CreateFromConnectionString(this.Configuration.IoTHubConnectionString);

            await sc.SendAsync(deviceId, message);
        }

        internal async Task<Twin> ReplaceTwinAsync(string deviceId, Twin updatedTwin, string etag)
        {
            try
            {
                return await this.GetRegistryManager().ReplaceTwinAsync(deviceId, updatedTwin, etag);
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error replacing twin for device {deviceId}: {ex.ToString()}");
                throw;
            }
        }

        public async Task InitializeAsync()
        {
            if (!string.IsNullOrEmpty(this.Configuration.LeafDeviceSerialPort))
            {
                this.arduinoDevice = LoRaArduinoSerial.CreateFromPort(this.Configuration.LeafDeviceSerialPort);
            }
            else
            {
                TestLogger.Log("[WARN] Not serial port defined for test");
            }

            if (this.Configuration.CreateDevices)
            {
                try
                {
                    await this.CreateOrUpdateDevicesAsync();
                }
                catch (Exception ex)
                {
                    TestLogger.Log($"[ERR] Failed to create devices in IoT Hub. {ex.ToString()}");
                }
            }

            if (!string.IsNullOrEmpty(this.Configuration.IoTHubEventHubConnectionString) && this.Configuration.NetworkServerModuleLogAssertLevel != LogValidationAssertLevel.Ignore)
            {
                this.IoTHubMessages = new EventHubDataCollector(this.Configuration.IoTHubEventHubConnectionString, this.Configuration.IoTHubEventHubConsumerGroup);
                await this.IoTHubMessages.StartAsync();
            }

            if (this.Configuration.UdpLog)
            {
                this.udpLogListener = new UdpLogListener(this.Configuration.UdpLogPort);
                this.udpLogListener.Start();
            }
        }

        private async Task CreateOrUpdateDevicesAsync()
        {
            TestLogger.Log($"Creating or updating IoT Hub devices...");
            var registryManager = this.GetRegistryManager();
            foreach (var testDevice in this.GetAllDevices().Where(x => x.IsIoTHubDevice))
            {
                var deviceID = testDevice.DeviceID;
                if (!string.IsNullOrEmpty(this.Configuration.DevicePrefix))
                {
                    deviceID = string.Concat(this.Configuration.DevicePrefix, deviceID.Substring(this.Configuration.DevicePrefix.Length, deviceID.Length - this.Configuration.DevicePrefix.Length));
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
                        if (!deviceTwin.Properties.Desired.Contains(kv.Key) || deviceTwin.Properties.Desired[kv.Key].ToString() != kv.Value.ToString())
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
        internal TestDeviceInfo GetDeviceByPropertyName(string propertyName)
        {
            return (TestDeviceInfo)this.GetType().GetProperty(propertyName).GetValue(this);
        }
    }
}
