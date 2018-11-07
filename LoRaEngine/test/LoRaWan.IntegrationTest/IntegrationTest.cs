using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWan.IntegrationTest
{

    public enum LoraRegion 
    {
        EU,
        US

    }

    /// <summary>
    /// Integration test
    /// </summary>
    public class IntegrationTest : IClassFixture<IntegrationTestFixture>, IDisposable
    {
        private readonly IntegrationTestFixture testFixture;
        private LoRaWanClass lora;
        LoraRegion loraRegion = LoraRegion.EU;


        public IntegrationTest(IntegrationTestFixture testFixture)
        {
            this.testFixture = testFixture;
            this.lora = LoRaWanClass.CreateFromPort(testFixture.Configuration.LeafDeviceSerialPort);
        }

        public void Dispose()
        {            
            this.lora?.Dispose();
            this.lora = null;
            GC.SuppressFinalize(this);
        }

        async Task SetupLora()
        {
            if (this.loraRegion == LoraRegion.EU)
            {
                await lora.setDataRateAsync(LoRaWanClass._data_rate_t.DR6, LoRaWanClass._physical_type_t.EU868);
                await lora.setChannelAsync(0, 868.1F);
                await lora.setChannelAsync(1, 868.3F);
                await lora.setChannelAsync(2, 868.5F);
                await lora.setReceiceWindowFirstAsync(0, 868.1F);
                await lora.setReceiceWindowSecondAsync(868.5F, LoRaWanClass._data_rate_t.DR2);
            }
            else
            {
                await lora.setDataRateAsync(LoRaWanClass._data_rate_t.DR0, LoRaWanClass._physical_type_t.US915HYBRID);
            }

            await lora.setAdaptiveDataRateAsync(false);
            await lora.setDutyCycleAsync(false);
            await lora.setJoinDutyCycleAsync(false);
            await lora.setPowerAsync(14);
        }

        [Fact]
        public async Task Test_OTAA_Confirmed_And_Unconfirmed_Message()
        {
            Console.WriteLine($"Starting {nameof(Test_OTAA_Confirmed_And_Unconfirmed_Message)}");
                                      
            string appSKey = null;
            string nwkSKey = null;
            string devAddr = null;
            var deviceId = testFixture.Configuration.LeafDeviceOTAAId;

            Console.WriteLine($"Connection type: {LoRaWanClass._device_mode_t.LWOTAA.ToString()}, DeviceId: {deviceId}, DeviceAppEui: {testFixture.Configuration.LeafDeviceAppEui}, DeviceAppKey: {testFixture.Configuration.LeafDeviceAppKey}");

            await lora.setDeciveModeAsync(LoRaWanClass._device_mode_t.LWOTAA);
            await lora.setIdAsync(devAddr, deviceId, testFixture.Configuration.LeafDeviceAppEui);
            await lora.setKeyAsync(nwkSKey, appSKey, testFixture.Configuration.LeafDeviceAppKey);

            await SetupLora();         

            var joinSucceeded = false;
            for (var joinAttempt=1; joinAttempt <= 5; ++joinAttempt)
            {
                Console.WriteLine($"Join attempt #{joinAttempt}");
                joinSucceeded = await lora.setOTAAJoinAsync(LoRaWanClass._otaa_join_cmd_t.JOIN, 20000);
                if(joinSucceeded)
                    break;

                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            if (!joinSucceeded)
            {                
                Assert.True(joinSucceeded, "Join failed");
            }

            // wait for serial messages to come
            await Task.Delay(100);
            
            // After join: Expectation on serial
            // +JOIN: Network joined
            // +JOIN: NetID 010000 DevAddr 02:9B:0D:3E  
            //Assert.Contains("+JOIN: Network joined", this.lora.SerialLogs);            
            Assert.Contains(this.lora.SerialLogs,(s) => s.StartsWith("+JOIN: NetID", StringComparison.Ordinal));


            // TODO: Check with Mikhail why the device twin is not being saved
            // After join: Expectation on device twin
            // DevAddr 02:9B:0D:3E -> exists in device twin reported properties
            //var devAddressInformation = serialContent.Split(System.Environment.NewLine).FirstOrDefault(x => x.StartsWith("+JOIN: NetID 010000 DevAddr"));
            //var devAddress = string.Empty;
            //if(!string.IsNullOrEmpty(devAddressInformation))
            //{
            //    devAddress = devAddressInformation.Replace("+JOIN: NetID 010000 DevAddr", string.Empty).Trim();
            //}
            //var deviceTwin = await this.eventHubDataCollectorFixture.GetTwinAsync(deviceId);
          //  Assert.True(deviceTwin.Properties.Reported.Contains("devAddr"));

            Console.WriteLine("Join succeeded");

            this.lora.ClearSerialLogs();
            testFixture.Events?.ResetEvents();

            lora.transferPacket("100", 10);

            // wait for serial logs to be ready
            await Task.Delay(TimeSpan.FromMilliseconds(200));

            // After transferPacket: Expectation from serial
            // +MSG: Done            
            Assert.Contains("+MSG: Done", this.lora.SerialLogs);            

            if (testFixture.Events != null)
            {
                // After transferPacket: Expectation from Log
                // 72AAC86800430020: valid frame counter, msg: 1 server: 0
                // 72AAC86800430020: decoding with: DecoderTemperatureSensor port: 1
                // 72AAC86800430020: sent message '{"temperature": 100}' to hub
                Assert.True(
                    await testFixture.EnsureHasEvent((e, deviceIdFromMessage, messageBody) => e.Properties.ContainsKey("log") && messageBody == $"{deviceId}: valid frame counter, msg: 1 server: 0"),
                    "Could not find correct valid frame counter");

                Assert.True(
                    await testFixture.EnsureHasEvent((e, deviceIdFromMessage, messageBody) => e.Properties.ContainsKey("log") && messageBody.StartsWith($"{deviceId}: decoding with: DecoderTemperatureSensor port: ", StringComparison.Ordinal)),
                    "Expecting DecoderTemperatureSensor");

                Assert.True(
                    await testFixture.EnsureHasEvent((e, deviceIdFromMessage, messageBody) => e.Properties.ContainsKey("log") && messageBody == $"{deviceId}: sent message '{{\"temperature\": 100}}' to hub"),
                    "Expecting message sent in log");
            }
            else
            {
                Console.WriteLine("Ignoring iot hub d2c message checking");
            }

            this.lora.ClearSerialLogs();
            testFixture.Events?.ResetEvents();

            
            lora.transferPacketWithConfirmed(new Random().Next().ToString(), 10);

            // wait for serial logs to be ready
            await Task.Delay(TimeSpan.FromMilliseconds(200));

            // After transferPacketWithConfirmed: Expectation from serial
            // +CMSG: ACK Received
            Assert.Contains("+CMSG: ACK Received", this.lora.SerialLogs);

            /*
            // After transferPacketWithConfirmed: Expectation from Log
            // 72AAC86800430020: valid frame counter, msg: 2 server: 1
            // 72AAC86800430020: decoding with: DecoderTemperatureSensor port: 1
            // 72AAC86800430020: sent message '{"temperature": 50}' to hub
            Assert.True(
               await testFixture.EnsureHasEvent((e, deviceIdFromMessage, messageBody) => e.Properties.ContainsKey("log") && messageBody == $"{deviceId}: valid frame counter, msg: 2 server: 1"),
               "Could not find correct valid frame counter");

            Assert.True(
               await testFixture.EnsureHasEvent((e, deviceIdFromMessage, messageBody) => e.Properties.ContainsKey("log") && messageBody.StartsWith($"{deviceId}: decoding with: DecoderTemperatureSensor port:")),
               "Expecting DecoderTemperatureSensor");

            Assert.True(
                await testFixture.EnsureHasEvent((e, deviceIdFromMessage, messageBody) => e.Properties.ContainsKey("log") && messageBody == $"{deviceId}: sent message '{{\"temperature\": 50}}' to hub"),
                "Expecting message sent in log");
         */

            //cts.Cancel();
        }


        [Fact]
        public async Task Test_ABP_Confirmed_And_Unconfirmed_Message()
        {
            Console.WriteLine($"Starting {nameof(Test_ABP_Confirmed_And_Unconfirmed_Message)}");

            
            // Now open the port.            
            string appSKey = testFixture.Configuration.LeafDeviceABPAppSKey;
            string nwkSKey = testFixture.Configuration.LeafDeviceABPNetworkSKey;
            string devAddr = testFixture.Configuration.LeafDeviceABPAddr;
            var deviceId = testFixture.Configuration.LeafDeviceABPId;

            Console.WriteLine($"Connection type: {LoRaWanClass._device_mode_t.LWABP.ToString()}, DevAddr: {devAddr}, NetworkSKey: {nwkSKey}, AppSKey: {appSKey}");

            await lora.setDeciveModeAsync(LoRaWanClass._device_mode_t.LWABP);
            await lora.setIdAsync(devAddr, deviceId, null);
            await lora.setKeyAsync(nwkSKey, appSKey, null);


            await SetupLora(); 

           
            this.lora.ClearSerialLogs();
            testFixture.Events?.ResetEvents();

            lora.transferPacket("100", 10);

            // wait for serial logs to be ready
            await Task.Delay(TimeSpan.FromMilliseconds(100));


            // After transferPacket: Expectation from serial
            // +MSG: Done                        
            Assert.Contains("+MSG: Done", this.lora.SerialLogs);

            if (testFixture.Events != null)
            {
                // After transferPacket: Expectation from Log
                // 72AAC86800430020: valid frame counter, msg: 1 server: 0
                // 72AAC86800430020: decoding with: DecoderValueSensor port: 1
                // 72AAC86800430020: sent message '{"value": 100}' to hub
                Assert.True(
                    await testFixture.EnsureHasEvent((e, deviceIdFromMessage, messageBody) => 
                    e.Properties.ContainsKey("log") && messageBody.StartsWith($"{deviceId}: valid frame counter, msg: ", StringComparison.Ordinal)),
                    "Could not find correct valid frame counter");

                Assert.True(
                    await testFixture.EnsureHasEvent((e, deviceIdFromMessage, messageBody) => 
                    e.Properties.ContainsKey("log") && messageBody.StartsWith($"{deviceId}: decoding with: DecoderValueSensor port:", StringComparison.Ordinal)),
                    "Expecting DecoderValueSensor");

                Assert.True(
                    await testFixture.EnsureHasEvent((e, deviceIdFromMessage, messageBody) => 
                    e.Properties.ContainsKey("log") && messageBody == $"{deviceId}: sent message '{{\"value\": 100}}' to hub"),
                    "Expecting message sent in log");
            }
            else
            {
                Console.WriteLine("Ignoring iot hub d2c message checking");
            }


            this.lora.ClearSerialLogs();
            testFixture.Events?.ResetEvents();

            lora.transferPacketWithConfirmed("50", 10);

            // wait for serial logs to be ready
            await Task.Delay(TimeSpan.FromMilliseconds(200));

            // After transferPacketWithConfirmed: Expectation from serial
            // +CMSG: ACK Received
            Assert.Contains("+CMSG: ACK Received", this.lora.SerialLogs);

            /*
            leafDeviceLog.Clear();
            testFixture.Events.ResetEvents();


            lora.transferPacketWithConfirmed("50", 10);

            // After transferPacketWithConfirmed: Expectation from serial
            // +CMSG: ACK Received
            Assert.Contains("+CMSG: ACK Received", leafDeviceLog);

            // After transferPacketWithConfirmed: Expectation from Log
            // 72AAC86800430020: valid frame counter, msg: 2 server: 1
            // 72AAC86800430020: decoding with: DecoderValueSensor port: 1
            // 72AAC86800430020: sent message '{"value": 50}' to hub
            Assert.True(
               await testFixture.EnsureHasEvent((e, deviceIdFromMessage, messageBody) => 
               e.Properties.ContainsKey("log") && messageBody.StartsWith($"{deviceId}: valid frame counter, msg: ")),
               "Could not find correct valid frame counter");

            Assert.True(
               await testFixture.EnsureHasEvent((e, deviceIdFromMessage, messageBody) => 
               e.Properties.ContainsKey("log") && messageBody.StartsWith($"{deviceId}: decoding with: DecoderValueSensor port:")),
               "Expecting DecoderValueSensor");

            Assert.True(
                await testFixture.EnsureHasEvent((e, deviceIdFromMessage, messageBody) => 
                e.Properties.ContainsKey("log") && messageBody == $"{deviceId}: sent message '{{\"value\": 50}}' to hub"),
                "Expecting message sent in log");

            */

            //cts.Cancel();
        }

        [Fact]
        public async Task Test_InvalidDevEUI_OTAA_Join()
        {
            Console.WriteLine($"Starting {nameof(Test_InvalidDevEUI_OTAA_Join)}");
                           
            string appSKey = null;
            string nwkSKey = null;
            string devAddr = null;
            var deviceId = "BE7A0000000014FF";

            Console.WriteLine($"Connection type: {LoRaWanClass._device_mode_t.LWOTAA.ToString()}, DeviceId: {deviceId}, DeviceAppEui: {testFixture.Configuration.LeafDeviceAppEui}, DeviceAppKey: {testFixture.Configuration.LeafDeviceAppKey}");

            await lora.setDeciveModeAsync(LoRaWanClass._device_mode_t.LWOTAA);
            await lora.setIdAsync(devAddr, deviceId, testFixture.Configuration.LeafDeviceAppEui);
            await lora.setKeyAsync(nwkSKey, appSKey, testFixture.Configuration.LeafDeviceAppKey);

            await SetupLora();         

            var joinSucceeded = false;
            for (var joinAttempt=1; joinAttempt <= 2; ++joinAttempt)
            {
                Console.WriteLine($"Join attempt #{joinAttempt}");
                joinSucceeded = await lora.setOTAAJoinAsync(LoRaWanClass._otaa_join_cmd_t.JOIN, 20000);
                if(joinSucceeded)
                    break;

                await Task.Delay(TimeSpan.FromSeconds(5));

            }

            Assert.False(joinSucceeded, "Join succeeded with invalid devEUI");

            // After join: Expectation on serial
            // +JOIN: Join failed
            Assert.Contains("+JOIN: Join failed", this.lora.SerialLogs);            
        }
    }
}
