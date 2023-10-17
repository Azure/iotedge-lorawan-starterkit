
#include <LoRaWan.h>
// The current sample is using AS923-3 on three different frequencies
// Refer to the repo documentation to adapt it to other AS923 variations
// set to true to send confirmed data up messages
bool confirmed=true;
// application information, should be similar to what was provisioned in the device twins
char * deviceId ="47AAC86800430028";
char * appKey="Check your device's AppKey in IoT Hub";
char * appEui ="BE7A0000000014E2";

/*
iot hub OTAA tags for deviceid: 47AAC86800430028
      "desired": {
      "AppEUI": "BE7A0000000014E2",
      "AppKey": "Check your device's AppKey in IoT Hub",
      "GatewayID" :"",
      "SensorDecoder" :"DecoderValueSensor"
      },
  */

// set initial datarate and physical information for the device
_data_rate_t dr=DR5;
_physical_type_t physicalType=AS923 ;

// internal variables
char data[10];
char buffer[256];
int i=0;
int lastCall=0;

void setup(void)
{
  SerialUSB.begin(115200);
  while(!SerialUSB);
  lora.init();
  lora.setDeviceDefault();
  delay(1000);
  lora.setId(NULL,deviceId , appEui);
  lora.setKey(NULL, NULL, appKey);

  lora.setDeciveMode(LWOTAA);
  lora.setDataRate(dr, physicalType);

  lora.setChannel(0, 916.6);
  lora.setChannel(1, 916.8);
  lora.setChannel(2, 917.0);
  
  lora.setReceiceWindowFirst(2, 917.4);

  lora.setAdaptiveDataRate(false);
  lora.setDutyCycle(false);
  lora.setJoinDutyCycle(false);
  lora.setPower(2);

  while(!lora.setOTAAJoin(JOIN,20000));
}

void loop(void)
{
  if((millis()-lastCall)>5000){
    lastCall=millis();
    bool result = false;
    String packetString = "";
    packetString=String(i);
    SerialUSB.println(packetString);
    packetString.toCharArray(data, 10);

    if(confirmed)
      result = lora.transferPacketWithConfirmed(data, 10);
    else
      result = lora.transferPacket(data, 10);
    i++;

    if(result)
    {
      short length;
      short rssi;

      memset(buffer, 0, sizeof(buffer));
      length = lora.receivePacket(buffer, sizeof(buffer), &rssi);

      if(length)
      {
        SerialUSB.print("Length is: ");
        SerialUSB.println(length);
        SerialUSB.print("RSSI is: ");
        SerialUSB.println(rssi);
        SerialUSB.print("Data is: ");
        for(unsigned char i = 0; i < length; i ++)
        {
            SerialUSB.print( char(buffer[i]));
        }
        SerialUSB.println();
      }
    }
  }
}
