using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Azure.EventHubs;
using Newtonsoft.Json;
using Xunit;

namespace LoRaWan.IntegrationTest
{
    public class IntegrationTestFixture : IDisposable, IAsyncLifetime
    {
        public const string MESSAGE_IDENTIFIER_PROPERTY_NAME = "messageIdentifier";
        RegistryManager registryManager;

        public TestConfiguration Configuration { get; }

        public EventHubDataCollector NetworkServerLogEvents { get; private set; }
     
        public void ClearNetworkServerLogEvents() => this.NetworkServerLogEvents?.ResetEvents();


        // Device1_OTAA: used for join test only
        public TestDeviceInfo Device1_OTAA { get; }

        // Device2_OTAA: used for failed join (wrong devEUI)
        public TestDeviceInfo Device2_OTAA { get; }

        // Device3_OTAA: used for failed join (wrong appKey)
        public TestDeviceInfo Device3_OTAA { get; }
        

        // Device4_OTAA: used for OTAA confirmed & unconfirmed messaging
        public TestDeviceInfo Device4_OTAA { get; }


        // Device5_ABP: used for ABP confirmed & unconfirmed messaging
        public TestDeviceInfo Device5_ABP { get; }

        // Device6_ABP: used for ABP wrong devaddr
        public TestDeviceInfo Device6_ABP { get; }

        // Device7_ABP: used for ABP wrong nwkskey
        public TestDeviceInfo Device7_ABP { get; }

        public IntegrationTestFixture()
        {
            this.Configuration = TestConfiguration.GetConfiguration();

            if (!string.IsNullOrEmpty(Configuration.IoTHubEventHubConnectionString) && this.Configuration.NetworkServerModuleLogAssertLevel != NetworkServerModuleLogAssertLevel.Ignore)
            {
                this.NetworkServerLogEvents = new EventHubDataCollector(Configuration.IoTHubEventHubConnectionString, Configuration.IoTHubEventHubConsumerGroup);
                var startTask = this.NetworkServerLogEvents.Start();                
                startTask.ConfigureAwait(false).GetAwaiter().GetResult();
            }

            // Device1_OTAA: used for join test only
            this.Device1_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000001",
                AppEUI = "BE7A0000000014E3",
                AppKey = "8AFE71A145B253E49C3031AD068277A3",                
                GatewayID = this.Configuration.LeafDeviceGatewayID,
                RealDevice = true                            
            };

            // Device2_OTAA: used for failed join (wrong devEUI)
            this.Device2_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000002",
                AppEUI = "BE7A0000000014E3",
                AppKey = "8AFE71A145B253E49C3031AD068277A3",
                GatewayID = "itestArm1",
                SensorDecoder = "DecoderValueSensor",
                RealDevice = false,
            };

            // Device3_OTAA: used for failed join (wrong appKey)
            this.Device3_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000003",
                AppEUI = "BE7A0000000014E3",
                AppKey = "8AFE71A145B253E49C3031AD068277A3",
                GatewayID = "itestArm1",
                SensorDecoder = "DecoderValueSensor",
                RealDevice = true,
            };
            

            // Device4_OTAA: used for OTAA confirmed & unconfirmed messaging
            this.Device4_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000004",
                AppEUI = "BE7A0000000014E3",
                AppKey = "8AFE71A145B253E49C3031AD068277A3",
                GatewayID = "itestArm1",
                SensorDecoder = "DecoderValueSensor",
                RealDevice = true,
            };        


            // Device5_ABP: used for ABP confirmed & unconfirmed messaging
            this.Device5_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000005",
                AppEUI = "0000000000000005",
                GatewayID = "itestArm1",
                SensorDecoder = "DecoderValueSensor",
                RealDevice = true,
                AppSKey="2B7E151628AED2A6ABF7158809CF4F3C",
                NwkSKey="3B7E151628AED2A6ABF7158809CF4F3C",
                DevAddr="0028B1B0"
            };     

            // Device6_ABP: used for ABP wrong devaddr
            this.Device6_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000006",
                AppEUI = "0000000000000006",
                GatewayID = "itestArm1",
                SensorDecoder = "DecoderValueSensor",
                RealDevice = false,
                AppSKey="2B7E151628AED2A6ABF7158809CF4F3C",
                NwkSKey="3B7E151628AED2A6ABF7158809CF4F3C",
                DevAddr="0028B1B0"
            };  

            // Device7_ABP: used for ABP wrong nwkskey
            this.Device7_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000007",
                AppEUI = "0000000000000007",
                GatewayID = "itestArm1",
                SensorDecoder = "DecoderValueSensor",
                RealDevice = true,
                AppSKey="2B7E151628AED2A6ABF7158809CF4F3C",
                NwkSKey="3B7E151628AED2A6ABF7158809CF4F3C",
                DevAddr="0028B1B0"
            };        
        }

        internal string GetMessageIdentifier(EventData eventData) 
        {
            eventData.Properties.TryGetValue("messageIdentifier", out var actualMessageIdentifier);
            return actualMessageIdentifier?.ToString();
        }      

        // Validate the network server log for the existence of a message
        public async Task ValidateNetworkServerEventLog(string logMessageStart)
        {
            if (this.Configuration.NetworkServerModuleLogAssertLevel != NetworkServerModuleLogAssertLevel.Ignore)
            {
                var foundEvent = await this.FindNetworkServerEventLog((e, deviceID, messageBody) => messageBody.StartsWith(logMessageStart));
                if (this.Configuration.NetworkServerModuleLogAssertLevel == NetworkServerModuleLogAssertLevel.Error)
                {
                    Assert.True(foundEvent, $"Did not find '{logMessageStart}' in logs");
                }
                else if (this.Configuration.NetworkServerModuleLogAssertLevel == NetworkServerModuleLogAssertLevel.Warning)
                {
                    Console.WriteLine($"'{logMessageStart}' found in logs? {foundEvent}");
                }
            }
        }

        // Search the network server logs for a value
        public async Task<bool> FindNetworkServerEventLog(Func<EventData, string, string, bool> predicate)
        {
            for (int i = 0; i < this.Configuration.EnsureHasEventMaximumTries; i++)
            {
                foreach (var item in this.NetworkServerLogEvents.GetEvents())
                {
                    var bodyText = System.Text.Encoding.UTF8.GetString(item.Body);
                    item.SystemProperties.TryGetValue("iothub-connection-device-id", out var deviceId);
                    if (predicate(item, deviceId?.ToString(), bodyText))
                    {
                        return true;
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(this.Configuration.EnsureHasEventDelayBetweenReadsInSeconds));
            }

            return false;
        }

        public void Dispose()
        {
            this.NetworkServerLogEvents?.Dispose();
            this.NetworkServerLogEvents = null;
            this.registryManager?.Dispose();
            this.registryManager = null;

            GC.SuppressFinalize(this);
        }

        internal async Task<Twin> GetTwinAsync(string deviceId)
        {
            var rm = this.registryManager ?? (this.registryManager = RegistryManager.CreateFromConnectionString(this.Configuration.IoTHubConnectionString));
            return await rm.GetTwinAsync(deviceId);            
        }

        internal async Task<Twin> ReplaceTwinAsync(string deviceId, Twin updatedTwin, string etag)
        {
            try
            {
                var rm = this.registryManager ?? (this.registryManager = RegistryManager.CreateFromConnectionString(this.Configuration.IoTHubConnectionString));
                return await rm.ReplaceTwinAsync(deviceId, updatedTwin, etag);
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
                var devices = new TestDeviceInfo[] 
                {
                    this.Device1_OTAA,
                    this.Device2_OTAA,
                    this.Device3_OTAA,
                    this.Device4_OTAA,
                    this.Device5_ABP,
                    this.Device6_ABP,
                    this.Device7_ABP,
                };

                var registryManager = RegistryManager.CreateFromConnectionString(this.Configuration.IoTHubConnectionString);
                foreach (var testDevice in devices.Where(x => x.RealDevice))
                {
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
                            if (deviceTwin.Properties.Desired.Contains(kv.Key) ||
                            deviceTwin.Properties.Desired[kv.Key].ToString() != kv.Value)
                            {
                                Console.WriteLine($"Unexpected value for device {testDevice.DeviceID} twin property {kv.Key}");
                                
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
        }

        public Task DisposeAsync() => Task.FromResult(0);
    }
}
