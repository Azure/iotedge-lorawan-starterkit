using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Azure.EventHubs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LoRaWan.IntegrationTest
{
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


        public IntegrationTestFixture()
        {
            this.Configuration = TestConfiguration.GetConfiguration();

            SetupTestDevices();

            // Fix device ID if a prefix was defined (DO NOT MOVE THIS LINE ABOVE DEVICE CREATION)             
            if (!string.IsNullOrEmpty(Configuration.DevicePrefix))
            {
                foreach (var d in GetAllDevices())
                {
                    d.DeviceID = string.Concat(Configuration.DevicePrefix, d.DeviceID.Substring(Configuration.DevicePrefix.Length, d.DeviceID.Length - Configuration.DevicePrefix.Length));
                    if (!string.IsNullOrEmpty(d.AppEUI))                    
                        d.AppEUI = string.Concat(Configuration.DevicePrefix, d.AppEUI.Substring(Configuration.DevicePrefix.Length, d.AppEUI.Length - Configuration.DevicePrefix.Length));

                    if (!string.IsNullOrEmpty(d.AppSKey))                    
                        d.AppSKey = string.Concat(Configuration.DevicePrefix, d.AppSKey.Substring(Configuration.DevicePrefix.Length, d.AppSKey.Length - Configuration.DevicePrefix.Length));

                    if (!string.IsNullOrEmpty(d.NwkSKey))                    
                        d.NwkSKey = string.Concat(Configuration.DevicePrefix, d.NwkSKey.Substring(Configuration.DevicePrefix.Length, d.NwkSKey.Length - Configuration.DevicePrefix.Length));                                            
                }
            }
        }

        // Setup the test devices here
        void SetupTestDevices()
        {
            var gatewayID = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID") ?? this.Configuration.LeafDeviceGatewayID;
          
            // Device1_OTAA: used for join test only
            this.Device1_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000001",
                AppEUI = "BE7A0000000014E3",
                AppKey = "8AFE71A145B253E49C3031AD068277A3",                
                GatewayID = gatewayID,
                IsIoTHubDevice = true                            
            };

            // Device2_OTAA: used for failed join (wrong devEUI)
            this.Device2_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000002",
                AppEUI = "BE7A0000000014E3",
                AppKey = "8AFE71A145B253E49C3031AD068277A3",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = false,
            };

            // Device3_OTAA: used for failed join (wrong appKey)
            this.Device3_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000003",
                AppEUI = "BE7A00000000FFE3",
                AppKey = "8AFE71A145B253E49C3031AD068277A3",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
            };
            

            // Device4_OTAA: used for OTAA confirmed & unconfirmed messaging
            this.Device4_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000004",
                AppEUI = "BE7A0000000014E3",
                AppKey = "8AFE71A145B253E49C3031AD068277A3",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
            };        


            // Device5_ABP: used for ABP confirmed & unconfirmed messaging
            this.Device5_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000005",
                AppEUI = "0000000000000005",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey="2B7E151628AED2A6ABF7158809CF4F3C",
                NwkSKey="3B7E151628AED2A6ABF7158809CF4F3C",
                DevAddr="0028B1B0"
            };     

            // Device6_ABP: used for ABP wrong devaddr
            this.Device6_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000006",
                AppEUI = "0000000000000006",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = false,
                AppSKey="2B7E151628AED2A6ABF7158809CF4F3C",
                NwkSKey="3B7E151628AED2A6ABF7158809CF4F3C",
                DevAddr="0028B1B1",
            };  

            // Device7_ABP: used for ABP wrong nwkskey
            this.Device7_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000007",
                AppEUI = "0000000000000007",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey="2B7E151628AED2A6ABF7158809CF4F3C",
                NwkSKey="3B7E151628AED2A6ABF7158809CF4F3C",
                DevAddr="0028B1B2"
            };  

            // Device8_ABP: used for ABP invalid nwkskey (mic fails)
            this.Device8_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000008",
                AppEUI = "0000000000000008",
                GatewayID = gatewayID,
                SensorDecoder = "DecoderValueSensor",
                IsIoTHubDevice = true,
                AppSKey="2B7E151628AED2A6ABF7158809CF4F3C",
                NwkSKey="3B7E151628AED2A6ABF7158809CF4F3C",
                DevAddr="0028B1B3"
            };    

            // Device9_OTAA: used for confirmed message & C2D
            this.Device9_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000009",
                AppEUI = "BE7A0000000014E3",
                AppKey = "8AFE71A145B253E49C3031AD068277A3",                
                GatewayID = gatewayID,
                IsIoTHubDevice = true                            
            };  

            // Device10_OTAA: used for unconfirmed message & C2D
            this.Device10_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000010",
                AppEUI = "BE7A0000000014E3",
                AppKey = "8AFE71A145B253E49C3031AD068277A3",                
                GatewayID = gatewayID,
                IsIoTHubDevice = true                            
            };  

            // Device11_OTAA: used for http decoder
            this.Device11_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000011",
                AppEUI = "BE7A0000000014E3",
                AppKey = "8AFE71A145B253E49C3031AD068277A3",                
                GatewayID = gatewayID,
                IsIoTHubDevice = true,                                      
                SensorDecoder = "http://sensordecodermodule/api/DecoderValueSensor",                           
            };  

            // Device12_OTAA: used for reflection based decoder
            this.Device12_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000012",
                AppEUI = "BE7A0000000014E3",
                AppKey = "8AFE71A145B253E49C3031AD068277A3",                
                GatewayID = gatewayID,
                IsIoTHubDevice = true,                                      
                SensorDecoder = "DecoderValueSensor",                           
            };  

            // Device13_OTAA: used for Join with wrong AppEUI
            this.Device13_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000013",
                AppEUI = "BE7A00000000FEE3",
                AppKey = "8AFE71A145B253E49C3031AD068277A3",                
                GatewayID = gatewayID,
                IsIoTHubDevice = true,                                      
                SensorDecoder = "DecoderValueSensor",                           
            };  
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
            }
        }
      
        public void ClearNetworkServerModuleLog()
        { 
            this.IoTHubMessages?.ResetEvents();
            this.udpLogListener?.ResetEvents();
        }

        bool disposed = false;
        public void Dispose()
        {
            Console.WriteLine($"{nameof(IntegrationTestFixture)} disposed");

            if (disposed)
                return;
            
            this.IoTHubMessages?.Dispose();
            this.IoTHubMessages = null;
            this.registryManager?.Dispose();
            this.registryManager = null;

            this.udpLogListener?.Dispose();
            this.udpLogListener = null;

            GC.SuppressFinalize(this);

            this.disposed = true;
        }

        RegistryManager GetRegistryManager()
        {
            return (this.registryManager ?? (this.registryManager = RegistryManager.CreateFromConnectionString(this.Configuration.IoTHubConnectionString)));

        }
        internal async Task<Twin> GetTwinAsync(string deviceId)
        {
            return await GetRegistryManager().GetTwinAsync(deviceId);            
        }

        internal async Task SendCloudToDeviceMessage(string deviceId, string messageText)
        {
            var msg = new Message(Encoding.UTF8.GetBytes(messageText));
            await SendCloudToDeviceMessage(deviceId, msg);
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
                return await GetRegistryManager().ReplaceTwinAsync(deviceId, updatedTwin, etag);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error replacing twin for device {deviceId}: {ex.ToString()}");
                throw;
            }
        }

        public async Task InitializeAsync()
        {
            if (this.Configuration.CreateDevices)
            {
               await CreateOrUpdateDevicesAsync();
            }

            if (!string.IsNullOrEmpty(Configuration.IoTHubEventHubConnectionString) && this.Configuration.NetworkServerModuleLogAssertLevel != IoTHubAssertLevel.Ignore)
            {
                this.IoTHubMessages = new EventHubDataCollector(Configuration.IoTHubEventHubConnectionString, Configuration.IoTHubEventHubConsumerGroup);
                await this.IoTHubMessages.StartAsync(); 
            }

            if (Configuration.UdpLog)
            {
                this.udpLogListener = new UdpLogListener(Configuration.UdpLogPort);
                this.udpLogListener.Start();
            }
        }

        private async Task CreateOrUpdateDevicesAsync()
        {
            var registryManager = GetRegistryManager();
            foreach (var testDevice in GetAllDevices().Where(x => x.IsIoTHubDevice))
            {
                var deviceID = testDevice.DeviceID;
                if (!string.IsNullOrEmpty(Configuration.DevicePrefix))
                {
                    deviceID = string.Concat(Configuration.DevicePrefix, deviceID.Substring(Configuration.DevicePrefix.Length, deviceID.Length - Configuration.DevicePrefix.Length));
                    testDevice.DeviceID = deviceID;
                }

                var getDeviceResult = await registryManager.GetDeviceAsync(testDevice.DeviceID);
                if (getDeviceResult == null)
                {
                    Console.WriteLine($"Device {testDevice.DeviceID} does not exist. Creating");
                    var device = new Device(testDevice.DeviceID);
                    var twin = new Twin(testDevice.DeviceID);                                            
                    twin.Properties.Desired = new TwinCollection(JsonConvert.SerializeObject(testDevice.GetDesiredProperties()));

                    Console.WriteLine($"Creating device {testDevice.DeviceID}");
                    await registryManager.AddDeviceWithTwinAsync(device, twin);
                }
                else 
                {
                    // compare device twin and make changes if needed
                    var deviceTwin = await registryManager.GetTwinAsync(testDevice.DeviceID);
                    var desiredProperties = testDevice.GetDesiredProperties();
                    foreach (var kv in desiredProperties)
                    {
                        if (!deviceTwin.Properties.Desired.Contains(kv.Key) || (string)deviceTwin.Properties.Desired[kv.Key] != kv.Value)
                        {
                            Console.WriteLine($"Unexpected value for device {testDevice.DeviceID} twin property {kv.Key}, expecting '{kv.Value}', actual is '{(string)deviceTwin.Properties.Desired[kv.Key]}'");
                            
                            var patch = new Twin();
                            patch.Properties.Desired = new TwinCollection(JsonConvert.SerializeObject(desiredProperties));
                            await registryManager.UpdateTwinAsync(testDevice.DeviceID, patch, deviceTwin.ETag);
                            Console.WriteLine($"Update twin for device {testDevice.DeviceID}");
                            break;
                        }
                    }
                }
            }
        }

        public Task DisposeAsync() => Task.FromResult(0);
    }
}
