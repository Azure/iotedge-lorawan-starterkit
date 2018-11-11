using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        // Device8_ABP: used for ABP invalid nwkskey (mic fails)
        public TestDeviceInfo Device8_ABP { get; }

        // Device9_OTAA: used for OTAA confirmed messages, C2D test
        public TestDeviceInfo Device9_OTAA { get; }

        // Device10_OTAA: used for OTAA unconfirmed messages, C2D test
        public TestDeviceInfo Device10_OTAA { get; }  

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
                DevAddr="0028B1B1",
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
                DevAddr="0028B1B2"
            };  

            // Device8_ABP: used for ABP invalid nwkskey (mic fails)
            this.Device8_ABP = new TestDeviceInfo()
            {
                DeviceID = "0000000000000008",
                AppEUI = "0000000000000008",
                GatewayID = "itestArm1",
                SensorDecoder = "DecoderValueSensor",
                RealDevice = true,
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
                GatewayID = this.Configuration.LeafDeviceGatewayID,
                RealDevice = true                            
            };  

            // Device10_OTAA: used for unconfirmed message & C2D
            this.Device10_OTAA = new TestDeviceInfo()
            {
                DeviceID = "0000000000000010",
                AppEUI = "BE7A0000000014E3",
                AppKey = "8AFE71A145B253E49C3031AD068277A3",                
                GatewayID = this.Configuration.LeafDeviceGatewayID,
                RealDevice = true                            
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
                var findResult = await this.FindNetworkServerEventLog((e, deviceID, messageBody) => messageBody.StartsWith(logMessageStart));
                if (this.Configuration.NetworkServerModuleLogAssertLevel == NetworkServerModuleLogAssertLevel.Error)
                {
                    var logs = string.Join("\n\t", findResult.Item2.TakeLast(5));
                    Assert.True(findResult.Item1, $"Did not find '{logMessageStart}' in logs [{logs}]");
                }
                else if (this.Configuration.NetworkServerModuleLogAssertLevel == NetworkServerModuleLogAssertLevel.Warning)
                {
                    if (findResult.Item1)
                    {
                        Console.WriteLine($"'{logMessageStart}' found in logs? {findResult.Item1}");
                    }
                    else
                    {
                        var logs = string.Join("\n\t", findResult.Item2.TakeLast(5));
                        Console.WriteLine($"'{logMessageStart}' found in logs? {findResult.Item1}. Logs: [{logs}]");
                    }
                }
            }
        }

        // Search the network server logs for a value
        //internal async Task<Tuple<bool, HashSet<string>>> FindNetworkServerEventLog(Func<EventData, string, string, bool> predicate)
        internal async Task<(bool found, HashSet<string> logs)> FindNetworkServerEventLog(Func<EventData, string, string, bool> predicate)      
        {
            var processedEvents = new HashSet<string>();
            for (int i = 0; i < this.Configuration.EnsureHasEventMaximumTries; i++)
            {
                if (i > 0)
                {
                    var timeToWait = i * this.Configuration.EnsureHasEventDelayBetweenReadsInSeconds;
                    Console.WriteLine($"Network server event log not found, attempt {i}/{this.Configuration.EnsureHasEventMaximumTries}, waiting {timeToWait} secs");
                    await Task.Delay(TimeSpan.FromSeconds(timeToWait));
                }

                foreach (var item in this.NetworkServerLogEvents.GetEvents())
                {
                    var bodyText = System.Text.Encoding.UTF8.GetString(item.Body);
                    processedEvents.Add(bodyText);
                    item.SystemProperties.TryGetValue("iothub-connection-device-id", out var deviceId);
                    if (predicate(item, deviceId?.ToString(), bodyText))
                    {
                        return (found: true, logs: processedEvents);// new Tuple<bool, HashSet<string>>(true, processedEvents);
                    }
                }
            }

            return (found: false, logs: processedEvents);
            //return new Tuple<bool, HashSet<string>>(false, processedEvents);
        }

        public void Dispose()
        {
            this.NetworkServerLogEvents?.Dispose();
            this.NetworkServerLogEvents = null;
            this.registryManager?.Dispose();
            this.registryManager = null;

            GC.SuppressFinalize(this);
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
                var devices = new TestDeviceInfo[] 
                {
                    this.Device1_OTAA,
                    this.Device2_OTAA,
                    this.Device3_OTAA,
                    this.Device4_OTAA,
                    this.Device5_ABP,
                    this.Device6_ABP,
                    this.Device7_ABP,
                    this.Device8_ABP,
                    this.Device9_OTAA,
                    this.Device10_OTAA,
                };

                var registryManager = GetRegistryManager();
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
        }

        public Task DisposeAsync() => Task.FromResult(0);
    }
}
