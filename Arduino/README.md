## Arduino Demo Code

__Please make sure the device twin tags are set correctly in IoT Hub, otherwise the sample won't work. Necessary tags are at the start of every sample file.ino.__


This samples were tested with the [Seeeduino LoRaWan boards](http://wiki.seeedstudio.com/Seeeduino_LoRAWAN/). The LoRaWan libraraie (referenced thru "LoRaWan.h" in the ino files) is coming when you select the platform as a Seeduino LoRaWan board.

![seeduino lorawan](/Docs/Pictures/seeduinolorawan.png)

If you are using another LoRaWan library, you will have to adjust this code as so far, all LoRaWan libraries are different from one manufacturer to another on Arduino platform. That said, adaptation shouldn't be too difficult and equivalent functions has to exist in all libraries.

This samples were tested with the Seeeduino LoRaWan boards. Samples are organized by regions as LoRaWan uses different frequences based on your geography. Please make sure you're using the sample from the correct geography.

1. **TransmissionTestOTAALoRa** - This is the most basic example. The sample perform an OTAA authentication and send a message to the gateway every 5 seconds. The sample also display on the serial interface any cloud to device message.

2. **TransmissionTestABPLoRa** - Same functionality as 1. but it uses ABP instead of OTAA.

3. **GPSOTAALoRa** - This sample sends GPS latitude and longitude information every 30 seconds using the onboard GPS. It uses OTAA activation. It uses OTAA activation to authenticate.

34 **TemperatureOTAALoRa** - This sample use the [Grove temperature sensor](http://wiki.seeedstudio.com/Grove-Temperature_Sensor/) to send temperature information every 30 seconds. It uses OTAA activation to authenticate. The sample also display on the serial interface any cloud to device message.