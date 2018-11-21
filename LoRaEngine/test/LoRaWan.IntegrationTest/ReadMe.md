# Integration Tests

This guide helps you to execute and author integration tests on your local environment.

## Requirements


```ascii
+----------+                          |
| Dev      |                          A
| Machine  |               +----------+
|          |               | IoT Edge |
+----------+       )       | PktFwd   |
  |              ) )       | NtwSrv   |
  | (usb)  |   ) ) )       +----------+
  |        A     ) )
 +---------+       )
 | Arduino |
 +---------+
```

* LoRaWan solution up and running (IoT Edge Device, IoT Hub, LoRa Keys Azure Function, Redis, etc.)
* Seeeduino LoRaWan device (leaf test device) connected via USB to a computer where the LoRaWan.IntegrationTest will run.
* Module LoRaWanNetworkSrvModule logging configured with following environment variables:
  * LOG_LEVEL: 1 or 2 (preferred)
  * LOG_TO_UDP: true
  * LOG_TO_UDP_ADDRESS: development machine IP address (ensure IoT Edge machine can ping it)
* Integration test configuration (in file `appsettings.local.json`) has UDP logging enabled `"UdpLog": "true"`

## Installation

1. Connect and setup Seeduino Arduino with the serial pass sketch

```c
void setup()
{
    Serial1.begin(9600);
    SerialUSB.begin(115200);
}

void loop()
{
    while(Serial1.available())
    {
        SerialUSB.write(Serial1.read());
    }
    while(SerialUSB.available())
    {
        Serial1.write(SerialUSB.read());
    }
}
```

2. Create/edit integration settings in file `appsettings.local.json`

The value of `LeafDeviceSerialPort` in Windows will be the COM port where the Arduino board is connected to (Arduino IDE displays it). On macos and/or Linux you can discover through `ls /dev/tty*` and/or `ls /dev/cu*` bash commands.

```json
{
  "testConfiguration": {
    "IoTHubEventHubConnectionString": "Endpoint=sb://xxxx.servicebus.windows.net/;SharedAccessKeyName=iothubowner;SharedAccessKey=xxx;EntityPath=xxxxx",
    "IoTHubEventHubConsumerGroup": "your-iothub-consumer-group",
    "IoTHubConnectionString": "HostName=xxxx.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=xxx",
    "EnsureHasEventDelayBetweenReadsInSeconds": "15",
    "EnsureHasEventMaximumTries": "5",
    "LeafDeviceSerialPort": "your-usb-port",
    "LeafDeviceGatewayID": "your-iot-edge-device-id",
    "CreateDevices": true,
    "NetworkServerModuleLogAssertLevel": "Error",
    "DevicePrefix": "your-two-letter-device-prefix"
  }
}
```

If the value of `CreateDevices` setting is true, running the tests will create/update devices in IoT Hub prior to executing tests. Devices will be created starting with ID "0000000000000001". The deviceID prefix can be modified by setting a value in `DevicePrefix` setting ('FF' &rarr; 'FF00000000000001').

## Creating a new test

Each test uses an unique device to ensure transmissions don't exceed LoRaWan regulations. Additionally, it makes it easier to track logs for each test.

To create a new device modify the IntegrationTestFixture class by:

1. Creating a new property of type `TestDeviceInfo` as (increment the device ids by 1):

```c#
// Device13_OTAA: used for wrong AppEUI OTAA join
public TestDeviceInfo Device13_OTAA { get; private set; }
```

2. Create the `TestDeviceInfo` instance in IntegrationTestFixture.SetupDevices() method

```c#
// Device13_OTAA: used for Join with wrong AppEUI
this.Device13_OTAA = new TestDeviceInfo()
{
    DeviceID = "0000000000000013",
    AppEUI = "BE7A00000000FEE3",
    AppKey = "8AFE71A145B253E49C3031AD068277A3",
    SensorDecoder = "DecoderValueSensor",

    // GatewayID property of the device
    GatewayID = gatewayID,

    // Indicates if the device exists in IoT Hub
    // Some tests don't require a device to actually exist
    IsIoTHubDevice = true,
};
```

3. Create the test method / fact in a test class. If a new test class is needed (to group logically test) read the section 'Creating Test Class'). Code should be similar to this:

```c#
// Tests using a invalid Network Session key, resulting in mic failed
// Uses Device8_ABP
[Fact]
public async Task Test_ABP_Invalid_NwkSKey_Fails_With_Mic_Error()
{
    var device = this.testFixture.Device8_ABP;
    Console.WriteLine($"Starting {nameof(Test_ABP_Invalid_NwkSKey_Fails_With_Mic_Error)} using device {device.DeviceID}");

    var nwkSKeyToUse = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
    Assert.NotEqual(nwkSKeyToUse, device.NwkSKey);
    await arduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
    await arduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
    await arduinoDevice.setKeyAsync(nwkSKeyToUse, device.AppSKey, null);

    await arduinoDevice.SetupLora(this.testFixture.Configuration.LoraRegion);

    await arduinoDevice.transferPacketAsync("100", 10);

    // THIS DELAY IS IMPORTANT!
    // Don't pollute radio transmission channel
    await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

    // Add here test logic
}
```

## Creating Test Class

Integration tests cannot be parallelized because they all share dependency to Arduino device. Those classes also rely on Udp and IoT Event Hub listeners that should be created once per test execution, not per test.

Therefore, when creating a new test class follow the guidelines:

* Use attribute `[Collection("ArduinoSerialCollection")` to ensure executing in serial
* inherit from `IClassFixture<IntegrationTestFixture>` to receive a singleton of `IntegrationTestFixture`. Make sure the constructor has a `IntegrationTestFixture` parameter.
* Implement IDisposable interface to ensure that the serial port is closed at the end of a single test execution

Example:

```c#
 // Tests xxxx
[Collection("ArduinoSerialCollection")] // Set the same collection to ensure execution in serial
public sealed class MyTest : IClassFixture<IntegrationTestFixture>, IDisposable
{
    private readonly IntegrationTestFixture testFixture;
    private LoRaArduinoSerial arduinoDevice;

    // Constructor receives the IntegrationTestFixture that is a singleton
    public MyTest(IntegrationTestFixture testFixture)
    {
        // Test fixture contains assertation helpers and devices
        // This class also has helpers to interact with IoT Hub Registry Manager (getting twins, create devices, etc.)
        this.testFixture = testFixture;

        // Create the lora arduino serial from a port
        this.arduinoDevice = LoRaArduinoSerial.CreateFromPort(testFixture.Configuration.LeafDeviceSerialPort);

        // Clear up the network server module log, to ensure noise from a previous test is removed
        this.testFixture.ClearNetworkServerModuleLog();
    }

    public void Dispose()
    {
        // Ensure that the port is closed for the next test
        this.lora?.Dispose();
        this.lora = null;
        GC.SuppressFinalize(this);
    }
}
```

## Asserting

Assertions and expectations are implemented in 3 levels.

### Arduino Serial logs

Serial logs from Arduino allow test cases to ensure the leaf device is receiving the correct response from the antena (LoRaPktFwd module).

Checks can be done the following way:

```c#
// After transferPacketWithConfirmed: Expectation from serial "+CMSG: ACK Received"
// It has retries to account for i/o delays
await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.lora.SerialLogs);
```

### LoRaWan Network Server Module logs

The network server module logs important execution steps. Those messages can be used to ensure expected actions happened in the network server module. This validation creates a tight dependency between tests and logging.

We might need to reavaluate it if the friction between code changes and breaking tests gets too high. An option is to have the Network server publish events when an operation happens and have the test create assertion on them (i.e. `{ "type": "otaajoin", "status": "succeeded", "deviceid": "xxx", "time": "a-date" }`).

Module logs can be listened from IoT Hub or UDP (experimental).

Validating against the module logs.

```c#
// Ensures that the message 0000000000000004: message '{"value": 51}' sent to hub is logged
// It contains retries to account for i/o delays
await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

```

### IoT Hub Device Message

For end to end validation we listen for device messages arriving in IoT Hub. Examples:

```c#
// Ensure device payload is available. It contains retries to account for i/o delays
// Data: {"value": 51}
var expectedPayload = $"{{\"value\":{msg}}}";
await this.testFixture.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);
```