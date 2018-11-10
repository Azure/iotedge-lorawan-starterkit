using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;

namespace LoRaWan.IntegrationTest
{

    // Tests OTAA join requests
    public sealed class JoinTest : IClassFixture<IntegrationTestFixture>, IDisposable
    {
        private readonly IntegrationTestFixture testFixture;
        private readonly LoraRegion loraRegion;
        private LoRaArduinoSerial lora;

        public JoinTest(IntegrationTestFixture testFixture)
        {
            this.testFixture = testFixture;
            this.loraRegion = LoraRegion.EU;
            this.lora = LoRaArduinoSerial.CreateFromPort(testFixture.Configuration.LeafDeviceSerialPort);
            this.testFixture.ClearNetworkServerLogEvents();
        }

        public void Dispose()
        {
            this.lora?.Dispose();
            this.lora = null;
            GC.SuppressFinalize(this);

        }

        // Ensures that an OTAA join will update the device twin
        [Fact]
        public async Task Join_With_Valid_Device_Updates_DeviceTwin()
        {   
            var device = this.testFixture.Device1_OTAA; 
            Console.WriteLine($"Starting {nameof(Join_With_Valid_Device_Updates_DeviceTwin)} using device {device.DeviceID}");      

            var twinBeforeJoin = await testFixture.GetTwinAsync(device.DeviceID);
            await lora.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await lora.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await lora.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await lora.SetupLora(this.loraRegion);

            var joinSucceeded = await lora.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

            if (!joinSucceeded)
            {                
                Assert.True(joinSucceeded, "Join failed");
            }

            // wait 1 second after joined
            await Task.Delay(TimeSpan.FromSeconds(1));
            
            // After join: Expectation on serial
            // +JOIN: Network joined
            // +JOIN: NetID 010000 DevAddr 02:9B:0D:3E  
            //Assert.Contains("+JOIN: Network joined", this.lora.SerialLogs);            
            Assert.Contains(this.lora.SerialLogs,(s) => s.StartsWith("+JOIN: NetID", StringComparison.Ordinal));


            // verify status in device twin
            await Task.Delay(TimeSpan.FromSeconds(60));
            var twinAfterJoin = await this.testFixture.GetTwinAsync(device.DeviceID);
            Assert.NotNull(twinAfterJoin);
            Assert.NotNull(twinAfterJoin.Properties.Reported);
            Assert.True(twinAfterJoin.Properties.Reported.Contains("FCntUp"), "Property FCntUp does not exist");
            Assert.True(twinAfterJoin.Properties.Reported.Contains("FCntDown"), "Property FCntDown does not exist");
            Assert.True(twinAfterJoin.Properties.Reported.Contains("NetId"), "Property NetId does not exist");
            Assert.True(twinAfterJoin.Properties.Reported.Contains("DevAddr"), "Property DevAddr does not exist");
            Assert.True(twinAfterJoin.Properties.Reported.Contains("DevNonce"), "Property DevNonce does not exist");
            Assert.True(twinAfterJoin.Properties.Reported.Contains("NwkSKey"), "Property NwkSKey does not exist");
            Assert.True(twinAfterJoin.Properties.Reported.Contains("AppSKey"), "Property AppSKey does not exist");
            Assert.True(twinAfterJoin.Properties.Reported.Contains("DevEUI"), "Property DevEUI does not exist");
            var devAddrBefore = (string)twinBeforeJoin.Properties.Reported["DevAddr"];
            var devAddrAfter = (string)twinAfterJoin.Properties.Reported["DevAddr"];
            var actualReportedDevEUI = (string)twinAfterJoin.Properties.Reported["DevEUI"];
            Assert.NotEqual(devAddrAfter, devAddrBefore);
            Assert.Equal(device.DeviceID, actualReportedDevEUI);
            Assert.True((twinBeforeJoin.Properties.Reported.Version) < (twinAfterJoin.Properties.Reported.Version), "Twin was not updated after join");
       
            // Verify network server log
            // 0000000000000001: join request received
            await testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: join request received");

            // 0000000000000001: querying the registry for device key
            await testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: querying the registry for device key");

            // 0000000000000001: saving join properties twins
            await testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: saving join properties twins");

            // 0000000000000001: join accept sent
            await testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: join accept sent");

        }


        // Ensure that a join with an invalid DevEUI fails
        // Does not need a real device, because the goal is no to have one that matches the DevEUI
        [Fact]
        public async Task Join_With_Wrong_DevEUI_Fails()
        {

            var device = this.testFixture.Device2_OTAA; 
            Console.WriteLine($"Starting {nameof(Join_With_Wrong_DevEUI_Fails)} using device {device.DeviceID}");       

            await lora.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await lora.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await lora.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await lora.SetupLora(this.loraRegion);

            var joinSucceeded = await lora.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.False(joinSucceeded, "Join suceeded for invalid DevEUI");

            if (this.testFixture.Configuration.NetworkServerModuleLogAssertLevel != NetworkServerModuleLogAssertLevel.Ignore)
            {
                // Expected messages in log
                // 0000000000000002: join request received
                await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: join request received");
                
                // 0000000000000002: querying the registry for device key
                await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: querying the registry for device key");
                
                // 0000000000000002: join request refused
                await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: join request refused");
            }
        }

        // Ensure that a join with an invalid AppKey fails
        [Fact]
        public async Task Join_With_Wrong_AppKey_Fails()
        {
            var device = this.testFixture.Device3_OTAA; 
            Console.WriteLine($"Starting {nameof(Join_With_Wrong_AppKey_Fails)} using device {device.DeviceID}");      
            var appKeyToUse = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            Assert.NotEqual(appKeyToUse, device.AppKey);
            await lora.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await lora.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await lora.setKeyAsync(device.NwkSKey, device.AppSKey, appKeyToUse);

            await lora.SetupLora(this.loraRegion);

            var joinSucceeded = await lora.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.False(joinSucceeded, "Join suceeded for invalid AppKey");

            if (this.testFixture.Configuration.NetworkServerModuleLogAssertLevel != NetworkServerModuleLogAssertLevel.Ignore)
            {
                // Expected messages in log
                // 0000000000000002: join request received
                await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: join request received");
                
                // 0000000000000002: querying the registry for device key
                await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: querying the registry for device key");
                
                // 0000000000000002: join request refused
                await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: join request refused");
            }
        }
    }
}