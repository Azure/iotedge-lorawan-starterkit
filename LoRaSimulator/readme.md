# LoRaWan device simulator

The LoRaWan device simulator allow to simulate both ABP devices and OTAA devices. The simulator is only available for class A devices. So devices which connect time to time to send and receive messages.

* ABP devices have a Dev Address as well ad the Network and Application Server Keys. They can directly send data without having to be joined.
* OTAA devices need to join first the network before being able to send data.

For more information regarding the various devices and the all up architecture, see the [LoRaEngine documentation](/LoRaEngine/README.md).

## Creating simulated devices

All simulated devices has to be created into the ```testconfig.json``` file located in [/LoRaSimulator/LoraSimulator](/LoRaSimulator/LoraSimulator) folder. Once compiled, the file is automatically copied with the simulator. So if you want to modify it once the simulator has been compiled, modify it in the build directory.

```json
    "Devices": [
    {
      "DevAddr": "260413AE",
      "DevEUI": "",
      "AppKey": "",
      "AppEUI": "",
      "NwkSKey": "99D58493D1205B43EFF938F0F66C339E",
      "AppSKey": "0A501524F8EA5FCBF9BDB5AD7D126F75",
      "DevNonce": "",
      "Interval": "10",
      "FrmCntUp": "1",
      "FrmCntDown": "0"
    },
    {
      "DevAddr": "",
      "DevEUI": "00AFEE7CF5ED6F1E",
      "AppKey": "8AFE71A145B253E49C3031AD068277A3",
      "AppEUI": "70B3D57ED00000DC",
      "NwkSKey": "",
      "AppSKey": "",
      "DevNonce": "",
      "Interval": "25",
      "FrmCntUp": "0",
      "FrmCntDown": "0"
    }
```

The above example shows one ABP device, the first one and one OTAA device, the second one. For ABP devices, mandatory fields are:

* ```DevAddr``` which does contains the Dev Address of the device. This address has to be unique for all the devices connected to one gateway.
* ```AppSKey``` which contains the Application Server Key used to encode the data payload send to the server.
* ```NwkSkey``` which contains the Network Server Key used to encode the MIC bytes used to verify the integrity of the packet sent from the device to the server.
* ```Interval``` is the interval in seconds for which the simulated device will send data to the server. This allow to generate messages to the server at various moments.
* ```FrmCntUp``` the frame number to start with for message going from the device to the gateway. This allow to start the frame counter at any number. Number has to be a uint32 and not larger.
* ```FrmCntDown``` the frame number to start with for message going from the gateway to the device. This allow to start the frame counter at any number. Number has to be a uint32 and not larger.
* other fields can be ignore or left empty

For OTAA devices, mandatory fields are the following:

* ```DevAddr``` **must** be an empty string
* ```DevEUI``` which does represent a unique device id. Please note that this unique device id **must** be the same device id as in your Azure IoT Hub. For OTAA devices, it is important to have it as a name.
* ```AppKey`` which is used to code the message sent from the gateway to the device one the request for join has been accepted.
* ```AppEUI``` which is used by the device to the gateway for the join request.
* ```Interval``` is the interval in seconds for which the simulated device will send data to the server. This allow to generate messages to the server at various moments.
* ```FrmCntUp``` the frame number to start with for message going from the device to the gateway. This allow to start the frame counter at any number. Number has to be a uint32 and not larger. For OTAA devices, it should always be 0 but you may want to test various scenarios.
* ```FrmCntDown``` the frame number to start with for message going from the gateway to the device. This allow to start the frame counter at any number. Number has to be a uint32 and not larger.
* other fields can be ignore or left empty. For OTAA devices, it should always be 0 but you may want to test various scenarios.
* Optional field: ```DevNonce``` which is normally a random number used for the join request from the device to the gateway.
* other fields can be ignore or left empty

**Note**: all the string representation are hexadecimal representations of bytes in bigendian except for ```Interval```, ```FrmCntUp``` and ```FrmCntDown``` which are decimal numbers.

## Creating the simulated gateway

The gateway definition is stored in the same file as for the simulated devices. ```testconfig.json``` file located in [/LoRaSimulator/LoraSimulator](/LoRaSimulator/LoraSimulator) folder. As for the simulated devices, the file is copied when building the solution with the executable, once done, if you want to adjust it, make sure you'll do it in the build directory.

```json
"rxpk": {
    "chan": 7,
    "rfch": 1,
    "freq": 903.700000,
    "stat": 1,
    "modu": "LORA",
    "datr": "SF10BW125",
    "codr": "4/5",
    "rssi": -17,
    "lsnr": 12.0
  }
```

All fields are mandatory for the simulation to work correctly. They **must** represent real possible values for a gateway. Please refer to the LoRaWan specifications for those values. Except having real values, there is no test done for network related elements. But the LoRaEngine module will use some of those values to adjust the answer which will be done to the simulator. This allow you to test all the region and channel usage in a correct way.

## Running the simulator and the LoRaEngine on the same machine as debug more

### Setting up LoRaEngine

If you want to run the LoRaEngine and the simulator on the same machine, with 2 different instances of VS Code or Visual Studio, it is possible. You'll have to setup different environment variables for the LoRaEngine. Go to the properties of the ```LoRaWanNetworkSrvModule```

![properties](/pictures/loraengineproperties.png)

then to the debug tab:

![properties details](/pictures/loraenginepropertiesdetails.png)

and add the following environment variables:

* TEST_DEBUG: true
* IOTEDGE_IOTHUBHOSTNAME: XXX.azure-devices.net where XXX is your IoT Hub registry name
* FACADE_AUTH_CODE: key. Where key is the secret key to access the function used to gather the device information like twins, get their own secret key to realize posting operation in Azure IoT Hub
* FACADE_SERVER_URL: https://XXX.azurewebsites.net/api where XXX is the name of your function. If you want as well to bebug the functions, you can do it. See full documentation [here](/LoRaEngine/README.md) if needed.
* LOG_LEVEL: 0 to get a full log view or any other level to get less information
* ENABLE_GATEWAY: false this will allow to directly post the data from the devices to Azure IoT Hub. If you have a gateway, you'll need to specify true and adjust other environment variable. See full documentation [here](/LoRaEngine/README.md) if needed.

### Setting up the LoRaSimulator

LoRaSimulator is ready to run directly. Just make sure the ```testconfig.json``` file is present in the same directory as the executable. See the devices and gateway section above for more information.

### Advices for breakpoints and debug

All the code is running in different threads and asynchronously. So make sure you pause either both environment or send only one message on one or the other direction to avoid collisions in data processing.

the UDP Port used by the LoRaEngine to listen is 1680. The port used by the LoRaSimulator is 1681 which does allow to run both on the same machine. You can change both ports in the source code.

It is as well possible to debug both in a Docker container. Both solution should be first deployed and attached to the containers to be able to debug.

