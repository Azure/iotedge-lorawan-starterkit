
#include <LoRaWan.h>
//set to true to send confirmed data up messages
bool confirmed=true;
//application information, should be similar to what was provisiionned in the device twins
char * deviceId ="47AAC86800430028";
char * appKey="8AFE71A145B253E49C3031AD068277A1";
char * appEui ="BE7A0000000014E2";

/*
iot hub OTAA tags for deviceid: 47AAC86800430028 
      "desired": {
      "AppEUI": "BE7A0000000014E2",
      "AppKey": "8AFE71A145B253E49C3031AD068277A1",
      "GatewayID" :"",
      "SensorDecoder" :"DecoderValueSensor"
      },
  */

//set initial datarate and physical information for the device
_data_rate_t dr=DR6;
_physical_type_t physicalType =EU868 ;

//internal variables
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

    lora.setId(NULL,deviceId , appEui);
    lora.setKey(NULL, NULL, appKey);
    
    lora.setDeciveMode(LWOTAA);
    lora.setDataRate(dr, physicalType);
    
    lora.setChannel(0, 868.1);
    lora.setChannel(1, 868.3);
    lora.setChannel(2, 868.5);
    
    lora.setReceiceWindowFirst(0, 868.1);
    
  lora.setAdaptiveDataRate(false);

  lora.setDutyCycle(false);
  lora.setJoinDutyCycle(false);

    
    lora.setPower(14);
    
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
        
        memset(buffer, 0, 256);
        length = lora.receivePacket(buffer, 256, &rssi);
        
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
