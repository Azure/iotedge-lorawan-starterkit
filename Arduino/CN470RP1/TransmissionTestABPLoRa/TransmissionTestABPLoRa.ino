
#include <LoRaWan.h>
// THOSE ARDUINO SAMPLE REQUIRES CHANGES IN THE ARDUINO CODE TO RUN DESCRIBED IN THE MAIN REPO
// THE CONCENTRATOR IS EXPECTED TO HAVE THE FOLLOWING FREQUENCIES ACTIVATED
// Cn470 Channels 1-4 : 470.3, 470.5, 470.7, 470.9
// Cn470 Channels 39-42: 478.1, 478.3, 478.5, 478.7
// please refer to the repo documentation for further information
//set to true to send confirmed data up messages
bool confirmed = true;
//application information, should be similar to what was provisiionned in the device twins
char * deviceId = "46AAC86800430028";
char * devAddr = "0228B1B1";
char * appSKey = "2B7E151628AED2A6ABF7158809CF4F3C";
char * nwkSKey = "3B7E151628AED2A6ABF7158809CF4F3C";


/*
  iot hub ABP tags for deviceid: 46AAC86800430028
    "desired": {
    "AppSKey": "2B7E151628AED2A6ABF7158809CF4F3C",
    "NwkSKey": "3B7E151628AED2A6ABF7158809CF4F3C",
    "DevAddr": "0228B1B1",
    "GatewayID" :"",
    "SensorDecoder" :"DecoderValueSensor"
    },
*/

//set initial datarate and physical information for the device
_data_rate_t dr = DR6;
_physical_type_t physicalType = CN470 ;

//internal variables
char data[10];
char buffer[256];
int i = 0;
int lastCall = 0;


void setup(void)
{
  SerialUSB.begin(115200);
  while (!SerialUSB);
  lora.init();
  delay(1000);
  lora.setDeviceDefault();

  lora.setId(devAddr, deviceId, NULL);
  lora.setKey(nwkSKey, appSKey, NULL);

  lora.setDeciveMode(LWABP);
  lora.setDataRate(dr, physicalType);

  // Disable Channels not used from the active channel list
  for (int i = 4 ; i< 39; i++){
    lora.setChannelOFF(i);
  }
  for (int i = 43 ; i< 96; i++){
    lora.setChannelOFF(i);
  }

  lora.setAdaptiveDataRate(false);

  lora.setDutyCycle(false);
  lora.setJoinDutyCycle(false);


  lora.setPower(2);


}

void loop(void)
{
  if ((millis() - lastCall) > 5000) {
    lastCall = millis();
    bool result = false;
    String packetString = "";
    packetString = String(i);
    SerialUSB.println(packetString);
    packetString.toCharArray(data, 10);

    if (confirmed)
      result = lora.transferPacketWithConfirmed(data, 10);
    else
      result = lora.transferPacket(data, 10);
    i++;

    if (result)
    {
      short length;
      short rssi;

      memset(buffer, 0, 256);
      length = lora.receivePacket(buffer, 256, &rssi);

      if (length)
      {
        SerialUSB.print("Length is: ");
        SerialUSB.println(length);
        SerialUSB.print("RSSI is: ");
        SerialUSB.println(rssi);
        SerialUSB.print("Data is: ");
        for (unsigned char i = 0; i < length; i ++)
        {
          SerialUSB.print( char(buffer[i]));

        }
        SerialUSB.println();
      }
    }
  }
}
