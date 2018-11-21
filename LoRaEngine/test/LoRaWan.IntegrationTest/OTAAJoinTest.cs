using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;

namespace LoRaWan.IntegrationTest
{

    // Tests OTAA join requests
    // OTAA joins requires the following information:
    // - DevEUI: a globally unique end-device identifier
    // - AppEUI: application identifier
    // - AppKey: a AES-128 key
    [Collection("ArduinoSerialCollection")] // run in serial
    public sealed class OTAAJoinTest : IClassFixture<IntegrationTestFixture>, IDisposable
    {
        private readonly IntegrationTestFixture testFixture;
        private readonly LoraRegion loraRegion;
        private LoRaArduinoSerial arduinoDevice;

        public OTAAJoinTest(IntegrationTestFixture testFixture)
        {
            this.testFixture = testFixture;
            this.loraRegion = LoraRegion.EU;
            this.arduinoDevice = LoRaArduinoSerial.CreateFromPort(testFixture.Configuration.LeafDeviceSerialPort);
            this.testFixture.ClearNetworkServerModuleLog();
        }

        public void Dispose()
        {
            this.arduinoDevice?.Dispose();
            this.arduinoDevice = null;
            GC.SuppressFinalize(this);

        }

        // Ensures that an OTAA join will update the device twin
        // Uses Device1_OTAA
        [Fact]
        public async Task OTAA_Join_With_Valid_Device_Updates_DeviceTwin()
        {   
            var device = this.testFixture.Device1_OTAA; 
            Console.WriteLine($"Starting {nameof(OTAA_Join_With_Valid_Device_Updates_DeviceTwin)} using device {device.DeviceID}");      

            var twinBeforeJoin = await testFixture.GetTwinAsync(device.DeviceID);
            await arduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await arduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await arduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await arduinoDevice.SetupLora(this.loraRegion);

            var joinSucceeded = await arduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

            Assert.True(joinSucceeded, "Join failed");

            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);
            
            // After join: Expectation on serial
            // +JOIN: Network joined
            // +JOIN: NetID 010000 DevAddr 02:9B:0D:3E  
            //Assert.Contains("+JOIN: Network joined", this.lora.SerialLogs);   
            await AssertUtils.ContainsWithRetriesAsync(
                (s) => s.StartsWith("+JOIN: NetID", StringComparison.Ordinal),
                this.arduinoDevice.SerialLogs
            );

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
        }


        // Ensure that a join with an invalid DevEUI fails
        // Does not need a real device, because the goal is no to have one that matches the DevEUI
        // Uses Device2_OTAA
        [Fact]
        public async Task OTAA_Join_With_Wrong_DevEUI_Fails()
        {
            var device = this.testFixture.Device2_OTAA; 
            Console.WriteLine($"Starting {nameof(OTAA_Join_With_Wrong_DevEUI_Fails)} using device {device.DeviceID}");

            await arduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await arduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await arduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await arduinoDevice.SetupLora(this.testFixture.Configuration.LoraRegion);

            var joinSucceeded = await arduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.False(joinSucceeded, "Join suceeded for invalid DevEUI");

            await this.arduinoDevice.WaitForIdleAsync();
        }
        
        // Ensure that a join with an invalid AppKey fails
        // Uses Device3_OTAA
        [Fact]
        public async Task OTAA_Join_With_Wrong_AppKey_Fails()
        {
            var device = this.testFixture.Device3_OTAA; 
            Console.WriteLine($"Starting {nameof(OTAA_Join_With_Wrong_AppKey_Fails)} using device {device.DeviceID}");      
            var appKeyToUse = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            Assert.NotEqual(appKeyToUse, device.AppKey);
            await arduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await arduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await arduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, appKeyToUse);

            await arduinoDevice.SetupLora(this.testFixture.Configuration.LoraRegion);

            var joinSucceeded = await arduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.False(joinSucceeded, "Join suceeded for invalid AppKey");

            await this.arduinoDevice.WaitForIdleAsync();
        }


        // Ensure that a join with an invalid AppKey fails
        // Uses Device13_OTAA
        [Fact]
        public async Task OTAA_Join_With_Wrong_AppEUI_Fails()
        {
            var device = this.testFixture.Device13_OTAA; 
            Console.WriteLine($"Starting {nameof(OTAA_Join_With_Wrong_AppEUI_Fails)} using device {device.DeviceID}");      
            var appEUIToUse = "FF7A00000000FCE3";
            Assert.NotEqual(appEUIToUse, device.AppEUI);
            await arduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await arduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, appEUIToUse);
            await arduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await arduinoDevice.SetupLora(this.testFixture.Configuration.LoraRegion);

            var joinSucceeded = await arduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.False(joinSucceeded, "Join suceeded for invalid AppKey");

            await this.arduinoDevice.WaitForIdleAsync();
        }
    }
}