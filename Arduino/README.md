## Arduino Demo Code

__Please make sure the device twin tags are set correctly in IoT Hub, otherwise the sample won't work. Necessary tags are at the start of every sample file.ino.__

This samples were tested with the Seeeduino LoRaWan boards.

1. **TransmissionTestOTAALoRa** - This is the most basic example. The sample perform an OTAA authentication and send a message to the gateway every 5 seconds. The sample also display on the serial interface any cloud to device message.

2. **GPSOTAALoRa** - This sample sends GPS latitude and longitude information every 30 seconds using the onboard GPS. It uses OTAA activation. It uses OTAA activation to authenticate.

3. **TemperatureOTAALoRa** - This sample use the [Grove temperature sensor](http://wiki.seeedstudio.com/Grove-Temperature_Sensor/) to send temperature information every 30 seconds. It uses OTAA activation to authenticate. The sample also display on the serial interface any cloud to device message.